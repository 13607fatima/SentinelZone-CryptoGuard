using CryptoGuard.Contracts;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CryptoGuard.Core;

public sealed class DurableSpool : ISpoolStore, IDisposable
{
    private readonly string root;
    private readonly string queue;
    private readonly AgentConfig config;
    private readonly FileStream lease;
    private sealed record Entry(string Uid, long Bytes, long ObservedTicks, bool Priority);
    private readonly SortedDictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> byUid = new(StringComparer.Ordinal);
    private readonly PriorityQueue<string, long> expiry = new();
    public DurableState State { get; }
    public string LastError { get; private set; } = "none";
    public long SizeBytes { get; private set; }

    public DurableSpool(AgentConfig config)
    {
        this.config = config;
        root = config.StateDirectory;
        Directory.CreateDirectory(root);
        queue = Path.Combine(root, "spool");
        Directory.CreateDirectory(queue);
        lease = new FileStream(Path.Combine(root, "writer.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        // Retained Windows migration originals and quarantine consume the disk budget too.
        SizeBytes = Directory.EnumerateFiles(queue).Where(p => p.EndsWith(".json", StringComparison.Ordinal) || p.EndsWith(".corrupt", StringComparison.Ordinal)).Sum(p => new FileInfo(p).Length);
        string path = Path.Combine(root, "identity.json");
        // Corrupt identity is a hard error. Never silently replace a deployed agent identity.
        State = File.Exists(path) ? JsonSerializer.Deserialize(File.ReadAllText(path), ContractJson.Default.DurableState) ?? throw new InvalidDataException("invalid_identity") : new();
        if (!Guid.TryParse(State.AgentId, out _) || State.Sequence < 0) throw new InvalidDataException("invalid_identity");
        SaveState();
        Recover();
    }
    public long NextSequence()
    {
        State.Sequence = checked(State.Sequence + 1);
        SaveState();
        return State.Sequence;
    }
    public void SaveState() => AtomicFile.Write(Path.Combine(root, "identity.json"), JsonSerializer.SerializeToUtf8Bytes(State, ContractJson.Default.DurableState));
    public bool Append(TelemetryEvent item)
    {
        if (!Guid.TryParse(item.EventUid, out _) || item.AgentId != State.AgentId || item.Sequence <= 0) throw new InvalidDataException("invalid_event_identity");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(item, ContractJson.Default.TelemetryEvent);
        bool priority = item.EventType is "risk" or "health";
        if (bytes.Length > 16 * 1024 * 1024) return Drop("event_too_large");
        try
        {
            PruneExpired(item.ObservedAt);
            if (byUid.TryGetValue(item.EventUid, out string? existing))
            {
                if (!File.ReadAllBytes(existing).AsSpan().SequenceEqual(bytes)) throw new InvalidDataException("event_uid_payload_collision");
                // A previous rename may have succeeded while directory fsync
                // failed. Retry that barrier before acknowledging durability.
                AtomicFile.SyncDirectory(queue);
                LastError = "none";
                return true;
            }
            long ceiling = priority ? config.SpoolLimitBytes : config.SpoolLimitBytes - config.RiskReserveBytes;
            // Only evict ordinary telemetry to make room for risk/health, never silently.
            if (priority)
                foreach (string old in entries.Where(e => !e.Value.Priority).Select(e => e.Key).ToArray())
                { if (SizeBytes + bytes.Length <= ceiling) break; RemoveAsLoss(old); }
            if (SizeBytes + bytes.Length > ceiling) return Reject("spool_limit", priority);
            string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            string name = $"{item.Sequence:D20}-{item.EventUid}-{hash}-{(priority ? "priority" : "normal")}";
            string pending = Path.Combine(queue, name + ".pending");
            if (File.Exists(pending)) File.Delete(pending);
            using (var file = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { file.Write(bytes); file.Flush(true); }
            string committed = Path.Combine(queue, name + ".event");
            File.Move(pending, committed);
            Index(committed, item, bytes.Length);
            AtomicFile.SyncDirectory(queue);
            LastError = "none";
            return true;
        }
        catch (InvalidDataException) { throw; }
        catch (IOException) { return Reject("disk_io_failure", priority); }
        catch (UnauthorizedAccessException) { return Reject("permission_denied", priority); }
    }
    private bool Reject(string reason, bool pendingPriority)
    {
        // The risk checkpoint owns a pending priority event. A deferred retry is not a dropped event.
        if (!pendingPriority) return Drop(reason);
        LastError = reason;
        return false;
    }
    private bool Drop(string reason)
    {
        State.DroppedEvents++;
        LastError = reason;
        try { SaveState(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        // Caller must surface this status; no raw exception message or payload may be logged.
        return false;
    }
    private void RemoveAsLoss(string path)
    { Remove(path); State.DroppedEvents++; SaveState(); }
    private void Index(string path, TelemetryEvent item, long bytes)
    {
        entries.Add(path, new(item.EventUid, bytes, item.ObservedAt.UtcTicks, item.EventType is "risk" or "health"));
        byUid.Add(item.EventUid, path);
        expiry.Enqueue(path, item.ObservedAt.UtcTicks);
        SizeBytes += bytes;
    }
    private void Remove(string path)
    {
        File.Delete(path);
        if (entries.Remove(path, out var entry)) { SizeBytes -= entry.Bytes; byUid.Remove(entry.Uid); }
        AtomicFile.SyncDirectory(queue);
        // ACKs/evictions leave stale heap nodes. Bound their retained memory.
        if (expiry.Count > entries.Count * 2 + 128)
        {
            expiry.Clear();
            foreach (var item in entries) expiry.Enqueue(item.Key, item.Value.ObservedTicks);
        }
    }
    public void PruneExpired(DateTimeOffset now)
    {
        long cutoff = now.UtcTicks - TimeSpan.FromHours(config.RetentionHours).Ticks;
        while (expiry.TryPeek(out string? path, out long ticks) && ticks < cutoff)
        {
            expiry.Dequeue();
            if (entries.ContainsKey(path)) RemoveAsLoss(path);
        }
    }
    private void Recover()
    {
        foreach (string path in Directory.EnumerateFiles(queue, "*.pending"))
        {
            try { ReadVerified(path); File.Move(path, Path.ChangeExtension(path, ".event"), false); }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
            { File.Delete(path); State.DroppedEvents++; }
            State.RecoveredPartialWrites++;
        }
        foreach (string path in Directory.EnumerateFiles(queue, "*.event").ToArray())
        {
            try
            {
                var item = ReadVerified(path);
                if (item.AgentId != State.AgentId || !Guid.TryParse(item.EventUid, out _) || item.Sequence <= 0 || byUid.ContainsKey(item.EventUid)) throw new InvalidDataException("spool_identity");
                Index(path, item, new FileInfo(path).Length);
                State.Sequence = Math.Max(State.Sequence, item.Sequence);
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
            { File.Delete(path); State.DroppedEvents++; State.RecoveredPartialWrites++; }
        }
        AtomicFile.SyncDirectory(queue);
        SaveState();
    }
    private static TelemetryEvent ReadVerified(string path)
    {
        if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new InvalidDataException("event_too_large");
        byte[] data = File.ReadAllBytes(path);
        string hash = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        if (!Path.GetFileName(path).Contains("-" + hash + "-", StringComparison.Ordinal)) throw new InvalidDataException("spool_checksum");
        return JsonSerializer.Deserialize(data, ContractJson.Default.TelemetryEvent) ?? throw new InvalidDataException("spool_json");
    }
    public IEnumerable<TelemetryEvent> ReadAll()
    { foreach (string path in entries.Keys.ToArray()) yield return ReadVerified(path); }
    public bool Acknowledge(string eventUid)
    {
        if (!Guid.TryParse(eventUid, out _)) return false;
        if (byUid.TryGetValue(eventUid, out string? path)) { Remove(path); return true; }
        return false;
    }
    public void Export(TextWriter output)
    { foreach (var item in ReadAll()) output.WriteLine(JsonSerializer.Serialize(item, ContractJson.Default.TelemetryEvent)); }
    public void Dispose() => lease.Dispose();
}
