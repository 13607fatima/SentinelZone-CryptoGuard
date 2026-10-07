using System.Text.Json;
using CryptoGuard.Compatibility.Windows.Contracts;

namespace CryptoGuard.Compatibility.Windows.Core;

// One atomically committed event per file. Incomplete writes cannot corrupt subsequent events.
public sealed class FileSpool : ISpoolStore
{
    private readonly string directory;
    private readonly AgentConfig config;
    private readonly string lossesPath;
    private sealed record Entry(string Path,string Uid,long Length,DateTime Expires);
    private readonly SortedDictionary<string,Entry> entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string,Entry> byUid = new(StringComparer.OrdinalIgnoreCase);
    private readonly PriorityQueue<Entry,DateTime> expiry = new();
    private long bytesOnDisk;
    public long DroppedEvents { get; private set; }
    public long RecoveredIncompleteWrites { get; private set; }
    public long Bytes => bytesOnDisk;
    public FileSpool(string directory, AgentConfig config)
    {
        this.directory = directory; this.config = config; Directory.CreateDirectory(directory);
        lossesPath = Path.Combine(directory, "losses.txt");
        if (File.Exists(lossesPath) && long.TryParse(File.ReadAllText(lossesPath), out var losses)) DroppedEvents = losses;
        foreach (var file in Directory.GetFiles(directory, "*.tmp")) { File.Delete(file); RecoveredIncompleteWrites++; Lose(); }
        foreach (var file in Directory.GetFiles(directory,"*.json").Order(StringComparer.Ordinal))
        {
            try {
                var item=ReadFile(file);
                if(byUid.ContainsKey(item.EventUid))throw new InvalidDataException("Duplicate event UID.");
                Index(file,item.EventUid);
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException) { File.Move(file, file + ".corrupt"); Lose(); }
        }
        // Quarantine consumes quota too. It is retained for explicit inspection.
        bytesOnDisk=Directory.EnumerateFiles(directory).Where(f=>f!=lossesPath).Sum(f=>new FileInfo(f).Length);
    }
    private void Index(string path,string uid)
    {
        var info=new FileInfo(path);
        var entry=new Entry(path,uid,info.Length,info.LastWriteTimeUtc.AddDays(config.RetentionDays));
        entries.Add(path,entry);byUid.Add(uid,entry);expiry.Enqueue(entry,entry.Expires);
    }
    private static EventEnvelope ReadFile(string file)
    {
        if(new FileInfo(file).Length>16*1024*1024)throw new InvalidDataException("Event exceeds read limit.");
        var item=JsonSerializer.Deserialize(File.ReadAllBytes(file), WireJson.Default.EventEnvelope) ?? throw new InvalidDataException("Empty event.");
        if(!Guid.TryParse(item.EventUid,out _) || item.Sequence<1)throw new InvalidDataException("Invalid event identity.");
        return item;
    }
    private void Remove(Entry entry)
    {
        File.Delete(entry.Path);entries.Remove(entry.Path);byUid.Remove(entry.Uid);bytesOnDisk-=entry.Length;
    }
    private void Prune()
    {
        while(expiry.TryPeek(out var entry,out var date) && date<DateTime.UtcNow)
        {
            expiry.Dequeue();
            if(byUid.GetValueOrDefault(entry.Uid)!=entry)continue;
            Remove(entry);Lose();
        }
    }
    private void Lose() { DroppedEvents++; AtomicFile.Write(lossesPath, System.Text.Encoding.ASCII.GetBytes(DroppedEvents.ToString(System.Globalization.CultureInfo.InvariantCulture))); }
    public bool Append(EventEnvelope envelope, bool priority)
    {
        if (!Guid.TryParse(envelope.EventUid,out _)) throw new InvalidDataException("Invalid event UID.");
        var path = Path.Combine(directory, $"{envelope.Sequence:D20}-{envelope.EventUid}.json");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, WireJson.Default.EventEnvelope);
        if(bytes.Length>16*1024*1024)throw new InvalidDataException("Event exceeds write limit.");
        if(byUid.TryGetValue(envelope.EventUid,out var existing))
        {
            if(!File.ReadAllBytes(existing.Path).AsSpan().SequenceEqual(bytes))throw new InvalidDataException("Event UID collision.");
            // Recovery after append-before-checkpoint-clear: prove durability again.
            using var stream=new FileStream(existing.Path,FileMode.Open,FileAccess.Write,FileShare.Read);
            stream.Flush(true);return true;
        }
        Prune();
        var limit = priority ? config.SpoolMaxBytes : config.SpoolMaxBytes - config.RiskReserveBytes;
        if (Bytes + bytes.Length > limit) { if(!priority)Lose(); return false; }
        try { AtomicFile.Write(path, bytes);Index(path,envelope.EventUid);bytesOnDisk+=bytes.Length;return true; }
        catch (IOException) { if(!priority)Lose(); return false; }
    }
    public IEnumerable<EventEnvelope> Read() { foreach (var entry in entries.Values) yield return ReadFile(entry.Path); }
    public bool Acknowledge(string eventUid)
    {
        if (!Guid.TryParse(eventUid, out _)) return false;
        if(!byUid.TryGetValue(eventUid,out var entry))return false;
        Remove(entry);return true;
    }
    public void Export(string path)
    {
        using var writer = new StreamWriter(path, false, new System.Text.UTF8Encoding(false));
        foreach (var entry in Read()) writer.WriteLine(JsonSerializer.Serialize(entry, WireJson.Default.EventEnvelope));
    }
    public static void ExportSnapshot(string directory,string path)
    {
        var source=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        if(Path.GetFullPath(path).StartsWith(source,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Export destination cannot be in the spool.");
        // Read only: no identity lock, recovery, expiry, ACK, or quota changes.
        var files=Directory.GetFiles(directory,"*.json").Order(StringComparer.Ordinal).ToArray();
        using var writer=new StreamWriter(path,false,new System.Text.UTF8Encoding(false));
        foreach(var file in files)
        {
            EventEnvelope item;
            try { item=ReadFile(file); } catch(FileNotFoundException) { continue; }
            writer.WriteLine(JsonSerializer.Serialize(item,WireJson.Default.EventEnvelope));
        }
    }
}
public static class Delivery
{
    public static async Task<bool> TrySendAsync(ISpoolStore spool, IEventSink sink, EventEnvelope item, int attempt, CancellationToken ct)
    {
        if (await sink.SendAsync(item, ct)) return spool.Acknowledge(item.EventUid);
        var delay = Math.Min(300, Math.Pow(2, Math.Clamp(attempt, 0, 8))) * (0.75 + Random.Shared.NextDouble() * .5);
        await Task.Delay(TimeSpan.FromSeconds(delay), ct); return false;
    }
}
