using CryptoGuard.Contracts;
using System.Text.Json;

namespace CryptoGuard.Core;

// A write failure here is fatal to this run: reload the last atomic checkpoint
// on restart instead of continuing with an uncertain in-memory lifecycle.
public sealed class RiskCheckpointException(Exception inner) : IOException("risk_checkpoint_io", inner);

public sealed class RiskCheckpointStore
{
    private readonly string path;
    private RiskCheckpoint current;
    public bool HasPendingEvent => current.PendingEvent is not null;

    public RiskCheckpointStore(string stateDirectory)
    {
        path = Path.Combine(stateDirectory, "risk-checkpoint.json");
        string legacy = Path.Combine(stateDirectory, "risk-state.json");
        current = File.Exists(path)
            ? JsonSerializer.Deserialize(File.ReadAllBytes(path), ContractJson.Default.RiskCheckpoint) ?? throw new InvalidDataException("risk_checkpoint")
            : new() { States = File.Exists(legacy) ? JsonSerializer.Deserialize(File.ReadAllBytes(legacy), ContractJson.Default.ListRiskState) ?? throw new InvalidDataException("legacy_risk_state") : [] };
        if (current.Version != 1 || current.States is null || current.PendingEvent is { EventType: not "risk" })
            throw new InvalidDataException("risk_checkpoint_version_or_payload");
    }

    public List<RiskState> RestoreStates() => JsonSerializer.Deserialize(
        JsonSerializer.SerializeToUtf8Bytes(current.States, ContractJson.Default.ListRiskState), ContractJson.Default.ListRiskState)!;

    public bool Commit(List<RiskState> states, TelemetryEvent? transition, ISpoolStore spool)
    {
        if (HasPendingEvent) throw new InvalidOperationException("risk_transition_pending");
        if (transition is not null && (transition.EventType != "risk" || JsonSerializer.SerializeToUtf8Bytes(transition, ContractJson.Default.TelemetryEvent).Length > 16 * 1024 * 1024))
            throw new InvalidDataException("invalid_risk_transition");
        // Persist post-evaluation state AND its unsent event in one atomic file.
        // The outbox holds at most one event; callers apply backpressure until
        // it is durably appended, preserving its original UUID across restarts.
        Save(new() { States = states, PendingEvent = transition });
        return FlushPending(spool);
    }

    public bool FlushPending(ISpoolStore spool)
    {
        if (current.PendingEvent is null) return true;
        if (!spool.Append(current.PendingEvent)) return false;
        Save(new() { States = current.States });
        return true;
    }

    private void Save(RiskCheckpoint checkpoint)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(checkpoint, ContractJson.Default.RiskCheckpoint);
        try { AtomicFile.Write(path, bytes); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw new RiskCheckpointException(ex); }
        current = JsonSerializer.Deserialize(bytes, ContractJson.Default.RiskCheckpoint)!;
    }
}
