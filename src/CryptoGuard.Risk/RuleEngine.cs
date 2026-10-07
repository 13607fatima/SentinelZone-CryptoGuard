using CryptoGuard.Contracts;
using System.Security.Cryptography;
using System.Text;
using CryptoGuard.ML;

namespace CryptoGuard.Risk;

public sealed class RuleEngine : IRiskEngine
{
    public const string Version = ReleaseVersions.Ruleset;
    private readonly AgentConfig config;
    private readonly ShadowForest? forest;
    private readonly string mlStatus;
    private readonly Dictionary<string, RiskState> states = new(StringComparer.Ordinal);
    public RuleEngine(AgentConfig config, IEnumerable<RiskState>? restored = null)
    {
        this.config = config;
        mlStatus=config.MlShadowEnabled?"model_not_configured":"disabled";
        if(config.MlShadowEnabled && config.MlModelPath is string path && config.MlModelSha256 is string hash)
        {
            try { forest=ShadowForest.Load(path,hash);mlStatus="ok"; }
            catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException) { mlStatus="model_rejected"; }
        }
        if (restored is not null) foreach (var s in restored.Take(config.MaxProcesses*2)) states[s.InstanceId] = s;
    }
    public List<RiskState> ExportState() => states.Values.OrderBy(s => s.InstanceId, StringComparer.Ordinal).ToList();

    public List<RiskResult> Evaluate(Snapshot snapshot)
    {
        var results = new List<RiskResult>();
        var present = new HashSet<string>(StringComparer.Ordinal);
        var validIocs = config.Iocs.Where(i => i.Confidence >= 0.9 && i.ExpiresAt > snapshot.ObservedAt && !string.IsNullOrWhiteSpace(i.Source)).ToArray();
        var ipIocs = validIocs.Where(i => i.Type == "ip").Select(i => i.Value).ToHashSet(StringComparer.Ordinal);
        var hashIocs = validIocs.Where(i => i.Type == "sha256").Select(i => i.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var networkMatches = snapshot.Connections.Where(c => c.ProcessInstanceId is not null && c.OwnerStatus == "ok" && c.State == "established" && ipIocs.Contains(c.RemoteAddress)).Select(c => c.ProcessInstanceId!).ToHashSet(StringComparer.Ordinal);
        foreach (var p in snapshot.Processes.OrderBy(p => p.ProcessInstanceId, StringComparer.Ordinal).Take(config.MaxProcesses))
        {
            present.Add(p.ProcessInstanceId);
            if (!states.TryGetValue(p.ProcessInstanceId, out var state) || state.BootId != snapshot.Host.BootId || snapshot.MonotonicMs < state.LastMs)
                states[p.ProcessInstanceId] = state = new() { InstanceId = p.ProcessInstanceId, BootId = snapshot.Host.BootId };
            bool gap = state.LastMs > 0 && snapshot.MonotonicMs - state.LastMs > config.SampleSeconds * 3000;
            if (gap) { state.HighLoadSinceMs = null; state.SuspectSinceMs = null; state.ClearSinceMs = null; state.CpuHistory.Clear(); }
            state.LastMs = snapshot.MonotonicMs;
            state.MissingSinceMs = null;
            var evidence = new List<Evidence>();
            bool load = p.CpuPercentHostCapacity.Status == "ok" && p.CpuPercentHostCapacity.Value >= config.HighCpuPercentHostCapacity || p.GpuPercent.Status == "ok" && p.GpuPercent.Value >= 70;
            if (load) state.HighLoadSinceMs ??= snapshot.MonotonicMs; else state.HighLoadSinceMs = null;
            if (state.HighLoadSinceMs is double highSince && snapshot.MonotonicMs - highSince >= 30000)
                evidence.Add(new("compute", 15, "sustained_process_compute_at_least_30_seconds", "process_metrics"));
            if (p.MiningArgumentCombination)
                evidence.Add(new("arguments", 25, "mining_protocol_and_algorithm_or_user_combination", "redacted_argument_features"));
            bool network = networkMatches.Contains(p.ProcessInstanceId);
            if (network) evidence.Add(new("network", 30, "process_connection_matches_trusted_unexpired_ip_ioc", "local_ioc_policy"));
            bool miningSpecific = p.MiningArgumentCombination || network;
            if (miningSpecific && p.UnusualWritableLocation)
                evidence.Add(new("executable", 10, "mining_evidence_in_unusual_writable_location", "executable_context"));
            if (miningSpecific && snapshot.Persistence.Any(e => e.ProcessInstanceId == p.ProcessInstanceId && e.Status == "ok"))
                evidence.Add(new("persistence", 10, "same_executable_has_persistence_with_mining_evidence", "persistence_inventory"));
            if (p.CpuPercentHostCapacity.Status == "ok" && p.CpuPercentHostCapacity.Value is double current)
            {
                if (miningSpecific && state.CpuHistory.Count >= 12)
                {
                    var sorted = state.CpuHistory.Order().ToArray();
                    double median = sorted[sorted.Length / 2];
                    if (current > Math.Max(median * 3, median + 15))
                        evidence.Add(new("anomaly", 10, p.Browser ? "browser_compute_anomaly_with_independent_mining_evidence" : "compute_anomaly_with_independent_mining_evidence", "bounded_cpu_history"));
                }
                state.CpuHistory.Add(current);
                if (state.CpuHistory.Count > 60) state.CpuHistory.RemoveAt(0);
            }
            else state.CpuHistory.Clear(); // Unknown samples must not bridge anomaly history.
            bool malicious = p.Sha256 is not null && hashIocs.Contains(p.Sha256);
            if(miningSpecific && (p.FileTrust.Status is "unsigned" or "invalid" || p.FileTrust.Integrity=="mismatch"))
                evidence.Add(new("trust",0,"untrusted_executable_with_independent_mining_evidence","executable_trust"));
            if (malicious) evidence.Add(new("malicious_hash", 95, "trusted_unexpired_malicious_sha256", "local_ioc_policy"));
            string authorization = config.AuthorizedWorkloads.Any(a => p.Sha256 is not null && a.Sha256.Equals(p.Sha256, StringComparison.OrdinalIgnoreCase) && a.ExePath == p.ExePath && (a.Publisher is null || a.Publisher == p.FileTrust.Publisher) && a.PolicyId.Length > 0) ? "authorized" : config.MiningAuthorization;
            int score = Math.Min(100, evidence.Sum(e => e.Points));
            // Explicit hash + path approval can suppress mining suspicion, never a known malicious hash.
            if (authorization == "authorized" && !malicious) score = Math.Min(score, 15);
            bool high = malicious || score >= 50 && evidence.Select(e => e.Group).Distinct().Count() >= 2 && miningSpecific;
            string severity = malicious || score >= 75 && high ? "critical" : high ? "high" : score >= 25 ? "medium" : "low";
            string old = state.Lifecycle;
            if (high)
            {
                if (old == "resolved") { state.AlertId = null; state.FirstSeen = null; state.SuspectSinceMs = null; }
                state.ClearSinceMs = null;
                state.SuspectSinceMs ??= snapshot.MonotonicMs;
                state.FirstSeen ??= snapshot.ObservedAt;
                state.LastSeen = snapshot.ObservedAt;
                state.AlertId ??= StableAlert(p.ProcessInstanceId, state.FirstSeen.Value);
                state.Lifecycle = malicious || snapshot.MonotonicMs - state.SuspectSinceMs.Value >= config.ConfirmSeconds * 1000 ? "confirmed" : "suspected";
                // A confirmed alert does not regress to suspected after a short data gap.
                if (old == "confirmed") state.Lifecycle = "confirmed";
            }
            else
            {
                state.SuspectSinceMs = null;
                if (score < 20 && p.CpuPercentHostCapacity.Status == "ok" && state.Lifecycle is "confirmed" or "suspected")
                {
                    state.ClearSinceMs ??= snapshot.MonotonicMs;
                    if (snapshot.MonotonicMs - state.ClearSinceMs >= config.ResolveSeconds * 1000) state.Lifecycle = "resolved";
                }
                else state.ClearSinceMs = null;
            }
            string assessment = malicious ? "known_malicious_executable" : state.Lifecycle == "confirmed" && miningSpecific ? (authorization == "prohibited" ? "unauthorized_mining_suspected" : "mining_activity_suspected") : miningSpecific ? "mining_indicators_observed" : "insufficient_evidence";
            results.Add(new()
            {
                ProcessInstanceId = p.ProcessInstanceId, Score = score, Severity = severity, Assessment = assessment,
                Authorization = authorization, Evidence = evidence.Select(e => e with { ProcessInstanceId = p.ProcessInstanceId }).ToList(), Coverage = snapshot.Coverage.ToList(),
                ResourceImpact = ResourceImpact(p, snapshot.Host), Reasons = evidence.Select(ReasonCode).Distinct().Order(StringComparer.Ordinal).ToList(),
                UnknownData = UnknownData(p, snapshot),
                Ml = forest?.Predict(Features.Extract(snapshot,p,state.HighLoadSinceMs is double start ? (snapshot.MonotonicMs-start)/1000 : load ? 0 : null)) ?? new() { Enabled=config.MlShadowEnabled,Status=mlStatus },
                Lifecycle = state.Lifecycle, AlertId = state.AlertId, FirstSeen = state.FirstSeen, LastSeen = state.LastSeen,
                LifecycleChanged = old != state.Lifecycle
            });
            // Keep the resolved alert and its identity in the checkpoint across a restart.
        }
        foreach (var entry in states.Where(e => !present.Contains(e.Key)).ToArray())
        {
            var s = entry.Value;
            if (s.Lifecycle == "observed") { states.Remove(entry.Key); continue; }
            if (s.Lifecycle == "resolved")
            {
                if (s.LastSeen is DateTimeOffset ended && snapshot.ObservedAt - ended > TimeSpan.FromHours(config.RetentionHours)) states.Remove(entry.Key);
                continue;
            }
            bool sameBoot = s.BootId == snapshot.Host.BootId;
            bool continuous = sameBoot && snapshot.MonotonicMs >= s.LastMs && snapshot.MonotonicMs - s.LastMs <= config.SampleSeconds * 3000;
            if (!continuous) s.MissingSinceMs = null;
            s.LastMs = snapshot.MonotonicMs;
            bool processCoverage = snapshot.Coverage.Count == 0 || snapshot.Coverage.Any(c => c.Name == "process" && c.Status == "ok");
            if (!processCoverage) { s.MissingSinceMs = null; continue; }
            s.MissingSinceMs ??= snapshot.MonotonicMs;
            if (sameBoot && snapshot.MonotonicMs - s.MissingSinceMs < config.ResolveSeconds * 1000) continue;
            s.Lifecycle = "resolved";
            s.LastSeen = snapshot.ObservedAt;
            results.Add(new() { ProcessInstanceId = s.InstanceId, Lifecycle = "resolved", LifecycleChanged = true, AlertId = s.AlertId, FirstSeen = s.FirstSeen, LastSeen = s.LastSeen,
                Assessment = "process_no_longer_observed", Reasons = ["PROCESS_NO_LONGER_OBSERVED"], Evidence = [new("lifecycle", 0, "missing_process_or_boot_changed", "snapshot_coverage")], Coverage = snapshot.Coverage.ToList() });
        }
        // Tombstones cannot grow indefinitely on hosts with rapid process churn.
        foreach (var old in states.Values.Where(s => s.Lifecycle == "resolved").OrderByDescending(s => s.LastSeen).Skip(config.MaxProcesses).ToArray()) states.Remove(old.InstanceId);
        return results;
    }
    private static int? ResourceImpact(ProcessSample p, HostSample host)
    {
        var values = new List<double>();
        if (p.CpuPercentHostCapacity is { Status: "ok", Value: double cpu }) values.Add(cpu);
        if (p.GpuPercent is { Status: "ok", Value: double gpu }) values.Add(gpu);
        if (p.RssBytes is { Status: "ok", Value: double rss } && host.MemoryTotalBytes is { Status: "ok", Value: > 0 }) values.Add(100 * rss / host.MemoryTotalBytes.Value.Value);
        return values.Count==0?null:(int)Math.Round(Math.Clamp(values.Max(), 0, 100), MidpointRounding.AwayFromZero);
    }
    private static string ReasonCode(Evidence e) => e.Group switch
    {
        "compute" => "SUSTAINED_COMPUTE", "arguments" => "MINING_ARGUMENTS", "network" => "MINING_NETWORK_EVIDENCE",
        "executable" => "SUSPICIOUS_PATH", "persistence" => "PERSISTENCE_PRESENT", "anomaly" => "BEHAVIOR_ANOMALY",
        "malicious_hash" => "KNOWN_MALICIOUS_HASH", "trust" => "UNTRUSTED_EXECUTABLE", _ => e.Group.ToUpperInvariant()
    };
    private static List<string> UnknownData(ProcessSample p, Snapshot sample)
    {
        var unknown = new List<string>();
        if (p.CpuPercentHostCapacity.Status != "ok") unknown.Add("process_cpu");
        if (p.GpuPercent.Status != "ok") unknown.Add("process_gpu");
        if (p.FileTrust.Status is "unsupported" or "temporarily_unavailable" or "not_inspected") unknown.Add("executable_trust");
        if (!sample.Sensors.Any(s => s.Type == "temperature" && s.Reading.Status == "ok")) unknown.Add("temperature");
        return unknown;
    }
    private static string StableAlert(string instance, DateTimeOffset first)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(instance + "|" + first.ToString("O") + "|" + Version));
        return new Guid(hash.AsSpan(0, 16)).ToString("D");
    }
}
