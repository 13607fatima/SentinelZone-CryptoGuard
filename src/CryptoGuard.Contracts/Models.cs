using System.Text.Json;
using System.Text.Json.Serialization;

namespace CryptoGuard.Contracts;

public sealed record Metric(double? Value, string Unit, string Status, string Source)
{
    public static Metric Missing(string unit, string source, string status = "unsupported") => new(null, unit, status, source);
    public static Metric Ok(double value, string unit, string source) => double.IsFinite(value) ? new(value, unit, "ok", source) : Missing(unit, source, "temporarily_unavailable");
}
public sealed record Capability(string Name, string Status, string Source, string Detail);
public sealed record CollectorError(string Collector, string Status, string Code);
public sealed record FileTrust(string TrustType, string Status, string? Package, string? Integrity)
{
    public string? Publisher { get; init; }
    public uint? OwnerUid { get; init; }
    public uint? OwnerGid { get; init; }
    public string? Permissions { get; init; }
    public string? PackageVersion { get; init; }
    public string? PackageArchitecture { get; init; }
    public string? Origin { get; init; }
    public string OriginStatus { get; init; } = "unsupported";
    public string? ExecutableFormat { get; init; }
    public long? FileSizeBytes { get; init; }
}
public sealed record Connection(string NamespaceId, string Family, string LocalAddress, int LocalPort, string RemoteAddress, int RemotePort, string State, string Inode, string? ProcessInstanceId, string OwnerStatus);
public sealed record PersistenceEntry(string Source, string Location, string? Executable, string? ProcessInstanceId, string Status);
public sealed record Sensor(string SensorId, string DeviceId, string Name, string Type, Metric Reading)
{
    public string Label => Name;
    public string Source => Reading.Source;
    public string Unit => Reading.Unit;
    public string Status => Reading.Status;
    public double? Value => Reading.Value;
}
public sealed record Device(string DeviceId, string Vendor, string Name, string Source, string Scope);
public sealed class HostSample
{
    public string Platform { get; set; } = "linux";
    public string OsVersion { get; set; } = "unknown";
    public string Architecture { get; set; } = "x64";
    public string BootId { get; set; } = "unknown";
    public string Hostname { get; set; } = "redacted";
    public int LogicalCpuCount { get; set; } = 1;
    public Metric CpuPercent { get; set; } = Metric.Missing("percent_host_capacity", "proc.stat", "warm_up");
    public Metric MemoryUsedPercent { get; set; } = Metric.Missing("percent", "proc.meminfo");
    public Metric MemoryTotalBytes { get; set; } = Metric.Missing("bytes", "proc.meminfo");
    public Metric NetworkReceiveBytesPerSecond { get; set; } = Metric.Missing("bytes_per_second", "proc.net.dev", "warm_up");
    public Metric NetworkTransmitBytesPerSecond { get; set; } = Metric.Missing("bytes_per_second", "proc.net.dev", "warm_up");
    public Metric IdleSeconds { get; set; } = Metric.Missing("seconds", "desktop.session");
    public Metric HostMemoryUsedPercent => MemoryUsedPercent;
    public Metric UserIdleSeconds => IdleSeconds;
    public int NewProcessesObserved { get; set; }
    public string ProcessStartCoverage { get; set; } = "snapshot_only_short_lived_processes_may_be_missed";
}
public sealed class ProcessSample
{
    public string ProcessInstanceId { get; set; } = "";
    public int Pid { get; set; }
    public int? Ppid { get; set; }
    public ulong StartTicks { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public string? ExePath { get; set; }
    public string ExeStatus { get; set; } = "unsupported";
    public string? Sha256 { get; set; }
    public string HashStatus { get; set; } = "unsupported";
    public string[] RedactedArguments { get; set; } = [];
    public string[] ArgumentsRedacted => RedactedArguments;
    public FileTrust Trust => FileTrust;
    public bool MiningArgumentCombination { get; set; }
    public bool UnusualWritableLocation { get; set; }
    public bool Browser { get; set; }
    public Metric CpuPercentHostCapacity { get; set; } = Metric.Missing("percent_host_capacity", "proc.pid.stat", "warm_up");
    public Metric RssBytes { get; set; } = Metric.Missing("bytes", "proc.pid.stat");
    public Metric GpuPercent { get; set; } = Metric.Missing("percent", "per_process_gpu");
    public FileTrust FileTrust { get; set; } = new("package_origin", "unsupported", null, null);
}
public sealed class Snapshot
{
    public TelemetrySummary? Summary { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public double MonotonicMs { get; set; }
    public double SampleDurationMs { get; set; }
    public HostSample Host { get; set; } = new();
    public List<ProcessSample> Processes { get; set; } = [];
    public List<Connection> Connections { get; set; } = [];
    public List<PersistenceEntry> Persistence { get; set; } = [];
    public List<Device> Devices { get; set; } = [];
    public List<Sensor> Sensors { get; set; } = [];
    public List<Capability> Coverage { get; set; } = [];
    public List<CollectorError> Errors { get; set; } = [];
}
public sealed record Evidence(string Group, int Points, string Reason, string Source)
{
    public string Type { get; init; } = "process";
    public string? ProcessInstanceId { get; init; }
    public string Detail => Reason;
}
public sealed class MlShadowResult
{
    public bool Enabled { get; set; }
    public string Mode { get; set; } = "shadow";
    public string? ModelVersion { get; set; }
    public string? Prediction { get; set; }
    public double? Score { get; set; }
    public double FeatureCoverage { get; set; }
    public string Status { get; set; } = "no_validated_model";
}
public sealed class RiskResult
{
    public string ProcessInstanceId { get; set; } = "";
    public int Score { get; set; }
    public int SecurityRisk => Score;
    public int? ResourceImpact { get; set; }
    public string AssessmentSource { get; set; } = "heuristic";
    public List<string> Reasons { get; set; } = [];
    public List<string> ReasonCodes => Reasons;
    public List<string> UnknownData { get; set; } = [];
    public MlShadowResult Ml { get; set; } = new();
    public string Severity { get; set; } = "low";
    public string Assessment { get; set; } = "insufficient_evidence";
    public string Authorization { get; set; } = "unknown";
    public string RulesetVersion { get; set; } = ReleaseVersions.Ruleset;
    public string? ModelVersion { get; set; } = "heuristic-1.0";
    public List<Evidence> Evidence { get; set; } = [];
    public List<Capability> Coverage { get; set; } = [];
    public string Lifecycle { get; set; } = "observed";
    public string? AlertId { get; set; }
    public DateTimeOffset? FirstSeen { get; set; }
    public DateTimeOffset? LastSeen { get; set; }
    public bool LifecycleChanged { get; set; }
}
public sealed record CollectorLatency(string Collector, double DurationMs, string Status);
public sealed class AgentHealth
{
    public List<CollectorError> CollectorErrors { get; set; } = [];
    public double CollectionDurationMs { get; set; }
    public List<CollectorLatency> CollectorLatency { get; set; } = [];
    public List<Capability> SensorAvailability { get; set; } = [];
    public long SpoolBytes { get; set; }
    public long DroppedEvents { get; set; }
    public long RecoveredPartialWrites { get; set; }
    public string Status { get; set; } = "ok";
}
public sealed class TelemetryEvent
{
    [JsonRequired]
    public string SchemaVersion { get; set; } = ReleaseVersions.Schema;
    [JsonRequired]
    public string EventType { get; set; } = "telemetry";
    [JsonRequired]
    public string EventUid { get; set; } = Guid.NewGuid().ToString("D");
    [JsonRequired]
    public string AgentId { get; set; } = "";
    [JsonRequired]
    public string AgentVersion { get; set; } = ReleaseVersions.Agent;
    [JsonRequired]
    public long Sequence { get; set; }
    [JsonRequired]
    public DateTimeOffset ObservedAt { get; set; }
    [JsonRequired]
    public double SampleDurationMs { get; set; }
    public Snapshot? Snapshot { get; set; }
    public List<RiskResult> Risk { get; set; } = [];
    public AgentHealth Health { get; set; } = new();
}
public sealed class Ioc
{
    public string Type { get; set; } = "ip";
    public string Value { get; set; } = "";
    public string Source { get; set; } = "";
    public double Confidence { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
public sealed class AuthorizedWorkload
{
    public string? Publisher { get; set; }
    public string Sha256 { get; set; } = "";
    public string ExePath { get; set; } = "";
    public string PolicyId { get; set; } = "";
}
public sealed class AgentConfig
{
    public string StateDirectory { get; set; } = "/var/lib/cryptoguard";
    public string CollectorSocket { get; set; } = "/run/cryptoguard/collector.sock";
    public int SampleSeconds { get; set; } = 5;
    public int NetworkSeconds { get; set; } = 10;
    public int SensorSeconds { get; set; } = 10;
    public int InventorySeconds { get; set; } = 900;
    public int ExportSeconds { get; set; } = 60;
    public int PersistenceSeconds { get; set; } = 900;
    public long SpoolLimitBytes { get; set; } = 268435456;
    public long RiskReserveBytes { get; set; } = 33554432;
    public int RetentionHours { get; set; } = 168;
    public bool IncludeHostname { get; set; }
    public int MaxProcesses { get; set; } = 8192;
    public int ConfirmSeconds { get; set; } = 60;
    public int ResolveSeconds { get; set; } = 60;
    public double HighCpuPercentHostCapacity { get; set; } = 15;
    public string MiningAuthorization { get; set; } = "unknown";
    public bool MlShadowEnabled { get; set; }
    public string? MlModelPath { get; set; }
    public string? MlModelSha256 { get; set; }
    public List<Ioc> Iocs { get; set; } = [];
    public List<AuthorizedWorkload> AuthorizedWorkloads { get; set; } = [];
}
public sealed class RiskState
{
    public string InstanceId { get; set; } = "";
    public string BootId { get; set; } = "";
    public double LastMs { get; set; }
    public double? HighLoadSinceMs { get; set; }
    public double? SuspectSinceMs { get; set; }
    public double? ClearSinceMs { get; set; }
    public string Lifecycle { get; set; } = "observed";
    public string? AlertId { get; set; }
    public DateTimeOffset? FirstSeen { get; set; }
    public DateTimeOffset? LastSeen { get; set; }
    public double? MissingSinceMs { get; set; }
    public List<double> CpuHistory { get; set; } = [];
}
public sealed class DurableState
{
    public string AgentId { get; set; } = Guid.NewGuid().ToString("D");
    public long Sequence { get; set; }
    public long DroppedEvents { get; set; }
    public long RecoveredPartialWrites { get; set; }
}
public sealed class RiskCheckpoint
{
    public int Version { get; set; } = 1;
    public List<RiskState> States { get; set; } = [];
    public TelemetryEvent? PendingEvent { get; set; }
}
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, WriteIndented = false, GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(TelemetryEvent))]
[JsonSerializable(typeof(RiskResult))]
[JsonSerializable(typeof(MlShadowResult))]
[JsonSerializable(typeof(Snapshot))]
[JsonSerializable(typeof(AgentConfig))]
[JsonSerializable(typeof(DurableState))]
[JsonSerializable(typeof(List<RiskState>))]
[JsonSerializable(typeof(List<RiskResult>))]
[JsonSerializable(typeof(RiskCheckpoint))]
public partial class ContractJson : JsonSerializerContext;
