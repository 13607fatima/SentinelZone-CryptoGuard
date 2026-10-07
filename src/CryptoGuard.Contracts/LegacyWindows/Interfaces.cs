namespace CryptoGuard.Compatibility.Windows.Contracts;

public interface IHostCollector { HostSample Collect(); }
public interface IProcessCollector { ProcessSample[] Collect(); }
public interface INetworkCollector { Connection[] Collect(IReadOnlyDictionary<int, string> processIds); }
public interface IPersistenceCollector { PersistenceEntry[] Collect(); }
public interface IIdleCollector { IdleSample Collect(); }
public interface IHardwareCollector { Task<Sensor[]> CollectAsync(CancellationToken cancellationToken); }
public interface IExecutableInspector { (string? Hash, string Trust, string? Signer) Inspect(string path); }
public interface IRiskEngine { RiskResult[] Evaluate(Snapshot snapshot, DateTimeOffset observedAt, double monotonicSeconds); }
public interface IEventSink { Task<bool> SendAsync(EventEnvelope envelope, CancellationToken cancellationToken); }
public interface ISpoolStore
{
    bool Append(EventEnvelope envelope, bool priority);
    IEnumerable<EventEnvelope> Read();
    bool Acknowledge(string eventUid);
    long Bytes { get; }
    long DroppedEvents { get; }
}
