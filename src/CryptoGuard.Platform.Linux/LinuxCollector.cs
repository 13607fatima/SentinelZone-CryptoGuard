using CryptoGuard.Contracts;
using CryptoGuard.Core;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CryptoGuard.Platform.Linux;

public sealed partial class LinuxCollector : IHostCollector, IProcessCollector, INetworkCollector, IPersistenceCollector, IIdleCollector, IHardwareCollector
{
    private readonly AgentConfig config;
    private readonly Dictionary<string, (ulong Ticks, double Ms)> processTimes = [];
    private readonly ExecutableInspector inspector = new();
    private readonly List<CollectorError> errors = [];
    private List<Connection> connections = [];
    private List<PersistenceEntry> persistence = [];
    private List<Device> devices = [];
    private List<Sensor> sensors = [];
    private double lastSample, lastNetwork = double.NegativeInfinity, lastHardware = double.NegativeInfinity, lastPersistence = double.NegativeInfinity;
    private ulong? cpuTotal, cpuIdle;
    private Dictionary<string, (ulong Rx, ulong Tx)> netCounters = [];
    private readonly long ticks = Native.sysconf(2), pageSize = Native.sysconf(30);
    private string agentId = Guid.Empty.ToString();
    private readonly string bootId = File.ReadAllText("/proc/sys/kernel/random/boot_id").Trim();
    private readonly int logicalCpus = Math.Max(1, File.ReadLines("/proc/stat").Count(s => s.Length > 3 && s.StartsWith("cpu", StringComparison.Ordinal) && char.IsDigit(s[3])));
    public LinuxCollector(AgentConfig config) { this.config = config; }

    public Snapshot Collect(string id)
    {
        agentId = id;
        errors.Clear();
        double now = Environment.TickCount64;
        var timer = Stopwatch.StartNew();
        var result = new Snapshot { ObservedAt = DateTimeOffset.UtcNow, MonotonicMs = now, SampleDurationMs = lastSample > 0 ? now - lastSample : 0 };
        result.Host = CollectHost(now);
        result.Processes = CollectProcesses(now, bootId);
        result.Host.NewProcessesObserved = NewProcessCount;
        if (now - lastNetwork >= config.NetworkSeconds * 1000) { connections = CollectConnections(result.Processes); lastNetwork = now; }
        var liveIds = result.Processes.Select(p => p.ProcessInstanceId).ToHashSet();
        // Cached connections may outlive the process: never attribute them to a reused PID.
        result.Connections = connections.Select(c => c.ProcessInstanceId is not null && !liveIds.Contains(c.ProcessInstanceId) ? c with { ProcessInstanceId = null, OwnerStatus = "temporarily_unavailable" } : c).ToList();
        bool suspicious = result.Processes.Any(p => p.MiningArgumentCombination);
        if (now - lastPersistence >= config.PersistenceSeconds * 1000 || suspicious && now - lastPersistence >= 30000)
        { persistence = CollectPersistence(result.Processes); lastPersistence = now; }
        result.Persistence = persistence.Select(e => e with { ProcessInstanceId = result.Processes.FirstOrDefault(p => p.ExePath is not null && p.ExePath == e.Executable)?.ProcessInstanceId }).ToList();
        if (now - lastHardware >= config.SensorSeconds * 1000) { (devices, sensors) = CollectHardware(); lastHardware = now; }
        result.Devices = devices;
        result.Sensors = sensors;
        result.Host.IdleSeconds = CollectIdle();
        result.Coverage = [
            new("process_cpu", errors.Any(e => e.Collector == "process" && e.Code == "process_limit") ? "partial" : "ok", "proc.pid.stat", "snapshot_delta_host_capacity_normalized"),
            new("process_starts", "partial", "proc.snapshot", "short_lived_processes_can_be_missed"),
            new("process_details", errors.Any(e => e.Collector == "process" && (e.Status == "permission_denied" || e.Code == "process_limit")) ? "partial" : "ok", "proc.pid", "kernel_access_policy_applies"),
            new("tcp_owner", errors.Any(e => e.Collector == "network") ? "partial" : "ok", "proc.pid.net_and_fd", "namespace_and_socket_inode_join"),
            new("network_bytes", "partial", "proc.net.dev", "host_network_namespace_only_not_per_process"),
            new("temperature", sensors.Any(s => s.Type == "temperature" && s.Reading.Status == "ok") ? "ok" : "unsupported", "hwmon_and_thermal", "labelled_sensor_not_assumed_cpu"),
            new("gpu", devices.Any(d => d.Source is "nvml" or "drm.sysfs") ? "partial" : "unsupported", "nvml_and_drm", "device_scope_only"),
            new("process_gpu", "unsupported", "none", "device_metrics_are_not_process_metrics"),
            new("idle", result.Host.IdleSeconds.Status, "logind", "aggregate_all_local_graphical_sessions_only"),
            new("persistence", errors.Any(e => e.Collector == "persistence") ? "partial" : "ok", "systemd_cron_xdg", "inventory_not_threat_verdict"),
            new("domain_ioc", "unsupported", "none", "no_passive_dns_source_no_reverse_dns_inference"),
            new("randomx", "unsupported", "none", "experimental_detector_disabled"),
            new("ml", "unsupported", "none", "no_validated_model_rules_only") ];
        result.Errors = errors.Distinct().Take(64).ToList();
        result.Errors.Add(new("collection_timing", "ok", $"elapsed_ms_{timer.ElapsedMilliseconds}"));
        lastSample = now;
        return result;
    }

    public HostSample CollectHost(double now)
    {
        var host = new HostSample { BootId = bootId, LogicalCpuCount = logicalCpus, Hostname = config.IncludeHostname ? Environment.MachineName : "redacted" };
        try
        {
            host.OsVersion = File.ReadLines("/etc/os-release").FirstOrDefault(s => s.StartsWith("PRETTY_NAME=", StringComparison.Ordinal))?[12..].Trim('"') ?? "unknown";
            var values = File.ReadLines("/proc/stat").First().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Skip(1).Take(8).Select(ulong.Parse).ToArray();
            ulong total = values.Aggregate(0UL, (a, b) => a + b), idle = values[3] + values[4];
            if (cpuTotal is ulong prior && cpuIdle is ulong priorIdle && total > prior && idle >= priorIdle)
                host.CpuPercent = Metric.Ok(Math.Clamp(100.0 * ((total - prior) - Math.Min(total - prior, idle - priorIdle)) / (total - prior), 0, 100), "percent_host_capacity", "proc.stat");
            cpuTotal = total; cpuIdle = idle;
            var mem = File.ReadLines("/proc/meminfo").Select(s => s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToDictionary(s => s[0].TrimEnd(':'), s => ulong.Parse(s[1]));
            host.MemoryTotalBytes = Metric.Ok(mem["MemTotal"] * 1024.0, "bytes", "proc.meminfo");
            if (mem.TryGetValue("MemAvailable", out ulong available)) host.MemoryUsedPercent = Metric.Ok(100.0 * (mem["MemTotal"] - Math.Min(available, mem["MemTotal"])) / mem["MemTotal"], "percent", "proc.meminfo");
            var counters = File.ReadLines("/proc/net/dev").Skip(2).Select(s => s.Split(':', 2)).Where(s => s.Length == 2 && s[0].Trim() != "lo")
                .Select(s => (Name: s[0].Trim(), Values: s[1].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))).ToDictionary(s => s.Name, s => (Rx: ulong.Parse(s.Values[0]), Tx: ulong.Parse(s.Values[8])));
            double elapsed = (now - lastSample) / 1000;
            if (lastSample > 0 && elapsed > 0 && counters.Keys.Order().SequenceEqual(netCounters.Keys.Order()))
            {
                bool reset = counters.Any(c => c.Value.Rx < netCounters[c.Key].Rx || c.Value.Tx < netCounters[c.Key].Tx);
                if (!reset)
                {
                    host.NetworkReceiveBytesPerSecond = Metric.Ok(counters.Sum(c => (double)(c.Value.Rx - netCounters[c.Key].Rx)) / elapsed, "bytes_per_second", "proc.net.dev");
                    host.NetworkTransmitBytesPerSecond = Metric.Ok(counters.Sum(c => (double)(c.Value.Tx - netCounters[c.Key].Tx)) / elapsed, "bytes_per_second", "proc.net.dev");
                }
            }
            netCounters = counters;
        }
        catch (Exception ex) when (Expected(ex)) { Error("host", ex); }
        return host;
    }

    public List<ProcessSample> CollectProcesses(double now, string boot)
    {
        inspector.BeginSample();
        var result = new List<ProcessSample>();
        var current = new Dictionary<string, (ulong Ticks, double Ms)>();
        var paths = Directory.EnumerateDirectories("/proc").Where(p => int.TryParse(Path.GetFileName(p), out _)).Order(StringComparer.Ordinal).Take(config.MaxProcesses + 1).ToArray();
        if (paths.Length > config.MaxProcesses) errors.Add(new("process", "partial", "process_limit"));
        foreach (var path in paths.Take(config.MaxProcesses))
        {
            try
            {
                var stat = ProcParsing.ParseProcessStat(ReadLimited(path + "/stat", 16384));
                string key = $"{boot}:{stat.Pid}:{stat.StartTicks}";
                string instance = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(agentId + ":" + key))).ToLowerInvariant();
                var p = new ProcessSample { Pid = stat.Pid, Ppid = stat.Ppid, StartTicks = stat.StartTicks, ProcessInstanceId = instance, RssBytes = Metric.Ok(Math.Max(0, stat.RssPages) * (double)pageSize, "bytes", "proc.pid.stat") };
                var uptime = double.Parse(File.ReadAllText("/proc/uptime").Split(' ')[0], CultureInfo.InvariantCulture);
                p.StartedAt = DateTimeOffset.UtcNow.AddSeconds(-uptime + stat.StartTicks / (double)ticks);
                if (processTimes.TryGetValue(key, out var prior) && ProcParsing.CpuPercent(stat.CpuTicks, prior.Ticks, (now - prior.Ms) / 1000, ticks, logicalCpus) is double percent)
                    p.CpuPercentHostCapacity = Metric.Ok(percent, "percent_host_capacity", "proc.pid.stat");
                current[key] = (stat.CpuTicks, now);
                try
                {
                    p.ExePath = Native.ReadLink(path + "/exe");
                    p.ExeStatus = p.ExePath is null ? "temporarily_unavailable" : "ok";
                    if (p.ExePath is not null)
                    {
                        p.UnusualWritableLocation = p.ExePath.StartsWith("/tmp/", StringComparison.Ordinal) || p.ExePath.StartsWith("/var/tmp/", StringComparison.Ordinal) || p.ExePath.StartsWith("/dev/shm/", StringComparison.Ordinal) || p.ExePath.Contains("/.cache/", StringComparison.Ordinal);
                        p.Browser = new[] { "firefox", "chrome", "chromium", "brave" }.Contains(Path.GetFileName(p.ExePath));
                        // Inspect the open process executable link, not a possibly replaced pathname.
                        (p.Sha256, p.HashStatus, p.FileTrust) = inspector.InspectProcess(path + "/exe", p.ExePath);
                    }
                    var redacted = Privacy.SummarizeArguments(ReadLimited(path + "/cmdline", 65536).Split('\0', StringSplitOptions.RemoveEmptyEntries));
                    p.RedactedArguments = redacted.Arguments;
                    p.MiningArgumentCombination = redacted.MiningCombination;
                }
                catch (Exception ex) when (Expected(ex)) { p.ExeStatus = Status(ex); Error("process", ex); }
                // Re-read identity after other reads to reject PID reuse races.
                if (ProcParsing.ParseProcessStat(ReadLimited(path + "/stat", 16384)).StartTicks == stat.StartTicks) result.Add(p);
            }
            catch (Exception ex) when (Expected(ex)) { Error("process", ex); }
        }
        NewProcessCount = current.Keys.Count(k => !processTimes.ContainsKey(k));
        processTimes.Clear();
        foreach (var (key, value) in current) processTimes[key] = value;
        return result;
    }
    public int NewProcessCount { get; private set; }
    internal static bool Expected(Exception e) => e is IOException or UnauthorizedAccessException or FormatException or OverflowException or InvalidOperationException or System.ComponentModel.Win32Exception;
    internal static string Status(Exception e) => e is UnauthorizedAccessException ? "permission_denied" : "temporarily_unavailable";
    private void Error(string collector, Exception e) { var error = new CollectorError(collector, Status(e), e.GetType().Name); if (errors.Count < 64 && !errors.Contains(error)) errors.Add(error); }
    internal static string ReadLimited(string path, int limit)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(file);
        char[] buffer = new char[limit];
        int read = reader.ReadBlock(buffer, 0, limit);
        return new string(buffer, 0, read);
    }
}
