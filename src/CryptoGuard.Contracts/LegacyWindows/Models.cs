using System.Text.Json;
using System.Text.Json.Serialization;

namespace CryptoGuard.Compatibility.Windows.Contracts;

public record Metric(double? Value, string Unit, string Status, string Source)
{
    public static Metric Missing(string unit, string source, string status = "temporarily_unavailable") => new(null, unit, status, source);
}
public record Coverage(string Name, string Status, string Source, string Detail);
public record HostSample(string Platform, string OsVersion, string Architecture, string BootId, string Hostname,
    int LogicalCpuCount, Metric CpuPercentHostCapacity, Metric MemoryUsedPercent, long? TotalMemoryBytes,
    Metric NetworkReceiveBytesPerSecond, Metric NetworkSendBytesPerSecond);
public record ProcessSample(string ProcessInstanceId, int Pid, int? Ppid, DateTimeOffset? StartedAt,
    string? ExePath, string? Sha256, string TrustType, string TrustStatus, string? Signer,
    Metric CpuPercentHostCapacity, long? RssBytes, string[] ArgumentFeatures, string Status);
public record Connection(int Pid, string? ProcessInstanceId, string Family, string LocalAddress, int LocalPort,
    string RemoteAddress, int RemotePort, string State, string Source);
public record PersistenceEntry(string Kind, string Location, string? ExecutablePath, string Status);
public record Sensor(string DeviceId, string DeviceName, string Id, string Name, string Type, string Unit,
    double? Value, string Status, string Source);
public record IdleSample(int? SessionId, double? IdleSeconds, string Status, string Source);
public record Evidence(string Group, string Code, int Weight, string Description);
public record RiskResult(string ProcessInstanceId, int Score, string Severity, string Assessment,
    string Authorization, string RulesetVersion, string? ModelVersion, Evidence[] Evidence, string Coverage);
public record Alert(string AlertId, string ProcessInstanceId, string State, DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen, RiskResult Risk);
public record Health(long SpoolBytes, long DroppedEvents, long RecoveredIncompleteWrites, double CollectionDurationMs,
    string[] CollectorErrors);
public record SummaryMetric(double? Minimum,double? Maximum,double? Mean,string Unit,int ValidSamples,int TotalSamples,double CoveredSeconds);
public record ProcessSummary(string ProcessInstanceId,SummaryMetric CpuPercentHostCapacity,SummaryMetric RssBytes);
public record TelemetrySummary(DateTimeOffset StartedAt,DateTimeOffset EndedAt,double DurationMs,int SampleCount,
    SummaryMetric HostCpuPercent,SummaryMetric HostMemoryUsedPercent,SummaryMetric ReceiveBytesPerSecond,
    SummaryMetric SendBytesPerSecond,ProcessSummary[] Processes);
public record Snapshot(HostSample Host, ProcessSample[] Processes, Connection[] Connections,
    PersistenceEntry[] Persistence, Sensor[] Sensors, IdleSample Idle, RiskResult[] Risks, Alert[] Alerts,
    Coverage[] Coverage, Health Health)
{
    public TelemetrySummary? Summary { get; init; }
}
public record EventEnvelope(string SchemaVersion, string EventType, string EventUid, string AgentId,
    string AgentVersion, long Sequence, DateTimeOffset ObservedAt, double SampleDurationMs, Snapshot Data);
public record AgentIdentity(string AgentId, long NextSequence);
public record MiningIoc(string Kind, string Value, string Source, string Confidence, DateTimeOffset ExpiresAt);
public record AuthorizationRule(string Sha256, string ExePath, string? Signer, string Decision);
public record CpuHistoryPoint(double MonotonicSeconds, double Cpu);
public record ProcessHistory(string ProcessInstanceId, CpuHistoryPoint[] Points);
public record AlertCheckpoint(Alert Alert, double SuspectedSince, double? ClearSince, double Last);
public record DetectionCheckpoint(string FormatVersion, string BootId, string RulesetVersion,
    double SavedMonotonicSeconds, ProcessHistory[] Histories, AlertCheckpoint[] Alerts)
{
    // Committed in the same atomic file as the resulting alert lifecycle.
    public EventEnvelope? PendingEvent { get; init; }
}
public record AgentConfig
{
    public bool MlShadowEnabled { get; init; }
    public string? MlModelPath { get; init; }
    public string? MlModelSha256 { get; init; }
    public int SampleSeconds { get; init; } = 5;
    public int NetworkSeconds { get; init; } = 10;
    public int HardwareSeconds { get; init; } = 10;
    public int SummarySeconds { get; init; } = 60;
    public int InventorySeconds { get; init; } = 900;
    public int PersistenceSeconds { get; init; } = 900;
    public long SpoolMaxBytes { get; init; } = 256 * 1024 * 1024;
    public long RiskReserveBytes { get; init; } = 32 * 1024 * 1024;
    public int RetentionDays { get; init; } = 7;
    public bool EnableHardware { get; init; } = false;
    public bool EnableCpuHardware { get; init; } = false;
    public MiningIoc[] Iocs { get; init; } = [];
    public AuthorizationRule[] Authorization { get; init; } = [];
    public void Validate()
    {
        if (SampleSeconds < 1 || NetworkSeconds < SampleSeconds || HardwareSeconds < SampleSeconds ||
            SummarySeconds < SampleSeconds || InventorySeconds < 30 || PersistenceSeconds < 30 ||
            RetentionDays < 1 || RiskReserveBytes < 0 || SpoolMaxBytes < 65536 || RiskReserveBytes >= SpoolMaxBytes)
            throw new InvalidDataException("Invalid collection or storage limits.");
        foreach (var rule in Authorization)
            if (rule.Sha256.Length != 64 || !rule.Sha256.All(Uri.IsHexDigit) || string.IsNullOrWhiteSpace(rule.ExePath) ||
                rule.Decision is not ("allowed" or "denied")) throw new InvalidDataException("Invalid authorization rule.");
        foreach (var ioc in Iocs)
            if (ioc.Kind is not ("ip" or "sha256") || ioc.Confidence != "high" || string.IsNullOrWhiteSpace(ioc.Source) ||
                ioc.Kind == "ip" && !System.Net.IPAddress.TryParse(ioc.Value,out _) ||
                ioc.Kind == "sha256" && (ioc.Value.Length != 64 || !ioc.Value.All(Uri.IsHexDigit)))
                throw new InvalidDataException("IOCs require a supported type, high confidence and provenance.");
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, WriteIndented = false,
    RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(EventEnvelope))]
[JsonSerializable(typeof(EventEnvelope[]))]
[JsonSerializable(typeof(AgentIdentity))]
[JsonSerializable(typeof(AgentConfig))]
[JsonSerializable(typeof(Sensor[]))]
[JsonSerializable(typeof(Coverage[]))]
[JsonSerializable(typeof(IdleSample))]
[JsonSerializable(typeof(RiskResult[]))]
[JsonSerializable(typeof(ProcessMetadata[]))]
[JsonSerializable(typeof(PersistenceEntry[]))]
[JsonSerializable(typeof(DetectionCheckpoint))]
public partial class WireJson : JsonSerializerContext { }
public record ProcessMetadata(int Pid, DateTimeOffset? StartedAt, int? Ppid, string[] ArgumentFeatures);
