using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CryptoGuard.Compatibility.Windows.Contracts;

namespace CryptoGuard.Compatibility.Windows.Core;

public static class AtomicFile
{
    public static void Write(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { stream.Write(bytes); stream.Flush(true); }
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
public sealed class StateStore : IDisposable
{
    private readonly string path;
    private readonly FileStream lease;
    private AgentIdentity identity;
    public string AgentId => identity.AgentId;
    public StateStore(string directory)
    {
        Directory.CreateDirectory(directory);
        lease = new FileStream(Path.Combine(directory, "agent.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        path = Path.Combine(directory, "identity.json");
        identity = File.Exists(path) ? JsonSerializer.Deserialize(File.ReadAllBytes(path), WireJson.Default.AgentIdentity)
            ?? throw new InvalidDataException("Identity is invalid; preserve it for recovery.") : new(Guid.NewGuid().ToString("D"), 1);
        if (!Guid.TryParse(identity.AgentId, out _) || identity.NextSequence < 1) throw new InvalidDataException("Invalid identity.");
        Save();
    }
    public long ReserveSequence()
    {
        var sequence = identity.NextSequence;
        identity = identity with { NextSequence = checked(sequence + 1) };
        Save(); // Reserve before append: crashes may leave gaps, never duplicate sequences.
        return sequence;
    }
    private void Save() => AtomicFile.Write(path, JsonSerializer.SerializeToUtf8Bytes(identity, WireJson.Default.AgentIdentity));
    public void Dispose() => lease.Dispose();
    public static string ProcessId(string agentId, string bootId, int pid, DateTimeOffset startedAt) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{agentId}|{bootId}|{pid}|{startedAt.UtcTicks}"))).ToLowerInvariant();
}

public static class Measurements
{
    public static double? Cpu(double? previous, double current, double elapsed, int logicalCpus) =>
        previous is null || elapsed <= 0 || logicalCpus < 1 || current < previous ? null :
        Math.Clamp(100 * (current - previous.Value) / (elapsed * logicalCpus), 0, 100);
    public static double? Rate(long? previous, long current, double elapsed) =>
        previous is null || elapsed <= 0 || current < previous ? null : (current - previous.Value) / elapsed;
}
