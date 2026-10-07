namespace CryptoGuard.Contracts;

public interface IHostCollector { HostSample CollectHost(double monotonicMs); }
public interface IProcessCollector { List<ProcessSample> CollectProcesses(double monotonicMs, string bootId); }
public interface INetworkCollector { List<Connection> CollectConnections(IReadOnlyList<ProcessSample> processes); }
public interface IPersistenceCollector { List<PersistenceEntry> CollectPersistence(IReadOnlyList<ProcessSample> processes); }
public interface IIdleCollector { Metric CollectIdle(); }
public interface IHardwareCollector { (List<Device> Devices, List<Sensor> Sensors) CollectHardware(); }
public interface IExecutableInspector { (string? Hash, string Status, FileTrust Trust) Inspect(string path); }
public interface IRiskEngine { List<RiskResult> Evaluate(Snapshot snapshot); }
public interface IEventSink { ValueTask<bool> AcceptAsync(TelemetryEvent item, CancellationToken cancellationToken); }
public interface ISpoolStore
{
    bool Append(TelemetryEvent item);
    IEnumerable<TelemetryEvent> ReadAll();
    bool Acknowledge(string eventUid);
    long SizeBytes { get; }
}
