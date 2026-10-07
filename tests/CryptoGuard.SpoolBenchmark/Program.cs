using CryptoGuard.Contracts;
using CryptoGuard.Core;
using CryptoGuard.Platform.Linux;
using System.Diagnostics;
using System.Text.Json;
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("linux")]

internal static class Program
{
    public static void Main(string[] args)
    {
        LinuxDurability.Initialize();
        string directory = Path.Combine(Path.GetTempPath(), "cg-spool-benchmark-" + Guid.NewGuid());
        LinuxDurability.PrivateDirectory(directory);
        var results = new List<object>();
        try
        {
            using var spool = new DurableSpool(new() { StateDirectory = directory });
            TelemetryEvent Event(int padding, string type) => new()
            {
                AgentId = spool.State.AgentId, Sequence = spool.NextSequence(), EventType = type,
                ObservedAt = DateTimeOffset.UtcNow, Snapshot = new() { Host = new() { OsVersion = new string('x', padding) } }
            };
            foreach (long target in new[] { 0L, 128L * 1024 * 1024, 224L * 1024 * 1024 })
            {
                while (spool.SizeBytes + 2 * 1024 * 1024 < target)
                    if (!spool.Append(Event(1024 * 1024, "health"))) throw new IOException("benchmark_fill");
                long baseline = spool.SizeBytes;
                var samples = new List<double>();
                var process = Process.GetCurrentProcess(); var cpu = process.TotalProcessorTime;
                for (int sample = 0; sample < 30; sample++)
                {
                    var watch = Stopwatch.StartNew();
                    var item = Event(2048, "telemetry");
                    if (!spool.Append(item) || !spool.Acknowledge(item.EventUid)) throw new IOException("benchmark_probe");
                    samples.Add(watch.Elapsed.TotalMilliseconds);
                }
                if (spool.SizeBytes != baseline) throw new IOException("benchmark_accounting");
                samples.Sort(); process.Refresh();
                results.Add(new { spool_bytes = baseline, samples = samples.Count, append_ack_mean_ms = samples.Average(), append_ack_p95_ms = samples[28], cpu_seconds = (process.TotalProcessorTime-cpu).TotalSeconds, rss_bytes = process.WorkingSet64 });
            }
        }
        finally { Directory.Delete(directory, true); }
        // Diagnostic tool only; reflection serialization is explicitly enabled
        // by its launch command, independently of the Native AOT agent.
        string json = JsonSerializer.Serialize(new { pass = true, method = "Managed Release build of the same Core, fsync enabled; per probe includes sequence persist, append and ACK. Not an end-to-end agent/72h benchmark.", results }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(args[0], json);
        Console.WriteLine(json);
    }
}
