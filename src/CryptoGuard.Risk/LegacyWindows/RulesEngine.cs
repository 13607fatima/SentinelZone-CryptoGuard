using CryptoGuard.Compatibility.Windows.Contracts;

namespace CryptoGuard.Compatibility.Windows.Risk;

public sealed class RulesEngine(AgentConfig config) : IRiskEngine
{
    public const string Version = "rules-0.22.0-alpha.3";
    private sealed record Point(double Time, double Cpu);
    private readonly Dictionary<string, Queue<Point>> windows = new();
    private double previousTime = -1;
    public ProcessHistory[] ExportHistory() => windows.Select(pair=>new ProcessHistory(pair.Key,
        pair.Value.Select(p=>new CpuHistoryPoint(p.Time,p.Cpu)).ToArray())).ToArray();
    public void RestoreHistory(ProcessHistory[] histories,double now)
    {
        windows.Clear();previousTime=-1;
        if(histories.Length>10000)throw new InvalidDataException("History limit exceeded.");
        foreach(var item in histories)
        {
            if(item is null || item.Points.Length>120 || string.IsNullOrWhiteSpace(item.ProcessInstanceId))throw new InvalidDataException("Invalid process history.");
            double last=-1;
            foreach(var point in item.Points)
            {
                if(point is null || !double.IsFinite(point.MonotonicSeconds) || point.MonotonicSeconds<0 || point.MonotonicSeconds<last || point.MonotonicSeconds>now ||
                    !double.IsFinite(point.Cpu) || point.Cpu<0 || point.Cpu>100)throw new InvalidDataException("Invalid CPU history point.");
                last=point.MonotonicSeconds;
            }
            // A collection gap cannot be counted as observed sustained load.
            if(item.Points.Length==0 || now-item.Points[^1].MonotonicSeconds>15)continue;
            windows[item.ProcessInstanceId]=new(item.Points.Where(p=>now-p.MonotonicSeconds<=120).Select(p=>new Point(p.MonotonicSeconds,p.Cpu)));
        }
    }
    public RiskResult[] Evaluate(Snapshot snapshot, DateTimeOffset observedAt, double monotonicSeconds)
    {
        if (!double.IsFinite(monotonicSeconds) || monotonicSeconds < previousTime) throw new InvalidDataException("Risk time must be monotonic.");
        previousTime = monotonicSeconds;
        var alive = snapshot.Processes.Select(p => p.ProcessInstanceId).ToHashSet();
        foreach (var id in windows.Keys.Where(id => !alive.Contains(id)).ToArray()) windows.Remove(id);
        var results = new List<RiskResult>();
        foreach (var p in snapshot.Processes)
        {
            if (!windows.TryGetValue(p.ProcessInstanceId, out var history)) windows[p.ProcessInstanceId] = history = new();
            if(history.Count>0 && monotonicSeconds-history.Last().Time>15) history.Clear();
            var prior = history.Count == 0 ? (double?)null : history.Average(s => s.Cpu);
            if (p.CpuPercentHostCapacity.Value is double cpu) history.Enqueue(new(monotonicSeconds, cpu));
            else history.Clear();
            while (history.Count > 120 || history.TryPeek(out var oldest) && monotonicSeconds - oldest.Time > 120) history.Dequeue();
            var evidence = new List<Evidence>();
            var sustained = p.CpuPercentHostCapacity.Value is not null && history.Count >= 2 && history.Last().Time - history.First().Time >= 30 && history.All(h => h.Cpu >= 25);
            if (sustained) evidence.Add(new("compute", "sustained_cpu", 15, "Process CPU uses at least 25% of host capacity for 30 seconds."));
            var flags = p.ArgumentFeatures.ToHashSet(StringComparer.Ordinal);
            var miningArgs = flags.Contains("stratum_protocol") && (flags.Contains("wallet_option") || flags.Contains("pool_option")) ||
                flags.Contains("mining_algorithm") && flags.Contains("pool_option") && flags.Contains("wallet_option");
            if (miningArgs) evidence.Add(new("arguments", "mining_argument_combination", 25, "Multiple mining-specific argument features."));
            var hashIoc = p.Sha256 is not null && config.Iocs.Any(i => i.Kind == "sha256" && i.ExpiresAt > observedAt &&
                i.Confidence == "high" && i.Value.Equals(p.Sha256, StringComparison.OrdinalIgnoreCase));
            var networkIoc = snapshot.Connections.Where(c => c.ProcessInstanceId == p.ProcessInstanceId && c.State == "Established")
                .Any(c => config.Iocs.Any(i => i.Kind == "ip" && i.ExpiresAt > observedAt && i.Confidence == "high" && i.Value == c.RemoteAddress));
            if (networkIoc) evidence.Add(new("network", "trusted_pool_ip", 30, "Established process connection matches a non-expired, high-confidence IOC."));
            var contextPath = p.ExePath?.Replace('/', '\\') ?? "";
            var writableContext = contextPath.Contains("\\AppData\\", StringComparison.OrdinalIgnoreCase) || contextPath.Contains("\\Temp\\", StringComparison.OrdinalIgnoreCase);
            if (writableContext && (miningArgs || networkIoc)) evidence.Add(new("executable", "user_path_with_mining_evidence", 10, "Mining evidence is associated with an executable in an unusual user-data location."));
            if ((miningArgs || networkIoc) && p.ExePath is not null && snapshot.Persistence.Any(x => x.ExecutablePath?.Equals(p.ExePath, StringComparison.OrdinalIgnoreCase) == true))
                evidence.Add(new("persistence", "linked_autostart", 10, "Autostart points to the same executable and accompanies mining evidence."));
            if ((miningArgs || networkIoc) && prior is double baseline && history.Count >= 6 && p.CpuPercentHostCapacity.Value > baseline + 30)
                evidence.Add(new("behavior", "cpu_increase", 10, "CPU increased more than 30 percentage points over the recent process baseline."));
            if (hashIoc) evidence.Add(new("hash", "trusted_malicious_hash", 100, "Executable SHA-256 matches a non-expired, high-confidence malicious hash IOC."));
            var score = Math.Min(100, evidence.Sum(e => e.Weight));
            var highEligible = hashIoc || (miningArgs || networkIoc) && evidence.Select(e => e.Group).Distinct().Count() >= 2;
            var severity = score >= 75 && highEligible ? "critical" : score >= 50 && highEligible ? "high" : score >= 25 ? "medium" : "low";
            var authorization = "unknown";
            if (p.Sha256 is not null && p.ExePath is not null)
            {
                var matches = config.Authorization.Where(a => a.Sha256.Equals(p.Sha256, StringComparison.OrdinalIgnoreCase) &&
                    a.ExePath.Equals(p.ExePath, StringComparison.OrdinalIgnoreCase) &&
                    (a.Signer is null || p.TrustStatus == "trusted_offline" && a.Signer.Equals(p.Signer, StringComparison.OrdinalIgnoreCase))).ToArray();
                authorization = matches.Any(a => a.Decision == "denied") ? "denied" : matches.Any(a => a.Decision == "allowed") ? "allowed" : "unknown";
            }
            var assessment = hashIoc ? "malicious_hash_match" : miningArgs || networkIoc ?
                authorization == "allowed" ? "authorized_mining_indicators" : authorization == "denied" ? "unauthorized_mining_indicators" : "mining_indicators_authorization_unknown"
                : "no_mining_specific_evidence";
            var coverage = p.Status != "ok" || p.CpuPercentHostCapacity.Value is null ? "partial" : "os_process_and_tcp_only";
            results.Add(new(p.ProcessInstanceId, score, severity, assessment, authorization, Version, null, evidence.ToArray(), coverage));
        }
        return results.ToArray();
    }
}

public sealed class AlertLifecycle(double confirmSeconds = 60, double resolveSeconds = 60)
{
    private sealed class State
    {
        public required Alert Alert;
        public double SuspectedSince;
        public double? ClearSince;
        public double Last;
    }
    private readonly Dictionary<string, State> states = new();
    public AlertCheckpoint[] ExportState()=>states.Values.Select(s=>new AlertCheckpoint(s.Alert,s.SuspectedSince,s.ClearSince,s.Last)).ToArray();
    public void RestoreState(AlertCheckpoint[] items,double now)
    {
        states.Clear();
        if(items.Length>10000)throw new InvalidDataException("Alert state limit exceeded.");
        foreach(var item in items)
        {
            if(item is null || !Guid.TryParse(item.Alert.AlertId,out _) || string.IsNullOrWhiteSpace(item.Alert.ProcessInstanceId) || item.Alert.State is not ("suspected" or "confirmed") ||
                !double.IsFinite(item.Last) || item.Last<0 || item.Last>now || !double.IsFinite(item.SuspectedSince) || item.SuspectedSince<0 || item.SuspectedSince>item.Last ||
                item.ClearSince is double clear && (!double.IsFinite(clear) || clear<0 || clear>item.Last))throw new InvalidDataException("Invalid alert state.");
            var gap=now-item.Last>15;
            states[item.Alert.ProcessInstanceId]=new State{Alert=item.Alert,SuspectedSince=gap?now:item.SuspectedSince,
                ClearSince=gap?null:item.ClearSince,Last=item.Last};
        }
    }
    public Alert[] Update(RiskResult[] risks, DateTimeOffset now, double seconds)
    {
        var transitions = new List<Alert>();
        var byId = risks.ToDictionary(r => r.ProcessInstanceId);
        foreach (var risk in risks)
        {
            bool suspicious = risk.Score >= 25 && (risk.Authorization != "allowed" || risk.Evidence.Any(e=>e.Code=="trusted_malicious_hash"));
            if (!states.TryGetValue(risk.ProcessInstanceId, out var state))
            {
                if (!suspicious) continue;
                state = new() { Alert = new(Guid.NewGuid().ToString("D"), risk.ProcessInstanceId, "suspected", now, now, risk), SuspectedSince = seconds, Last = seconds };
                states[risk.ProcessInstanceId] = state;
                transitions.Add(state.Alert);
            }
            if (seconds - state.Last > 15) { state.SuspectedSince = seconds;state.ClearSince=null; }
            state.Last = seconds;
            state.Alert = state.Alert with { LastSeen = now, Risk = risk };
            if (suspicious)
            {
                if (state.ClearSince is not null) state.SuspectedSince = seconds;
                state.ClearSince = null;
                if (state.Alert.State != "confirmed" && (risk.Evidence.Any(e => e.Code == "trusted_malicious_hash") || seconds - state.SuspectedSince >= confirmSeconds))
                { state.Alert = state.Alert with { State = "confirmed" }; transitions.Add(state.Alert); }
            }
            else if (risk.Score < 15 && risk.Coverage != "partial" || risk.Authorization == "allowed") state.ClearSince ??= seconds;
            else state.ClearSince = null;
        }
        foreach (var (id, state) in states.ToArray())
        {
            if (!byId.ContainsKey(id))
            {
                if(seconds-state.Last>15)state.ClearSince=null;
                state.ClearSince ??= seconds;state.Last=seconds;
            }
            if (state.ClearSince is double clear && seconds - clear >= resolveSeconds)
            { transitions.Add(state.Alert with { State = "resolved", LastSeen = now }); states.Remove(id); }
        }
        return transitions.ToArray();
    }
}
