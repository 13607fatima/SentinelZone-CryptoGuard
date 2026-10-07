using System.Diagnostics;
using System.Management;
using CryptoGuard.Compatibility.Windows.Contracts;
using CryptoGuard.Compatibility.Windows.Core;

namespace CryptoGuard.Platform.Windows;

public sealed class ProcessCollector(string agentId, string bootId, int cpuCount, IExecutableInspector inspector) : IProcessCollector
{
    private readonly Dictionary<string, (double Cpu, long Stamp)> previous = new();
    private Dictionary<int, ProcessMetadata> metadata = new();
    private int rotation;
    public void SetMetadata(ProcessMetadata[] items) => metadata = items.GroupBy(p => p.Pid).ToDictionary(g => g.Key, g => g.Last());
    public ProcessSample[] Collect()
    {
        var list = new List<ProcessSample>(); var alive = new HashSet<string>(); int inspections = 0;
        var batch=Process.GetProcesses().OrderBy(p=>p.Id).ToArray();
        var offset=batch.Length==0 ? 0 : rotation%batch.Length;rotation+=8;
        foreach (var process in batch.Skip(offset).Concat(batch.Take(offset)))
        {
            using (process)
            {
                try
                {
                    var start = new DateTimeOffset(process.StartTime.ToUniversalTime());
                    var id = StateStore.ProcessId(agentId, bootId, process.Id, start); alive.Add(id);
                    var now = Stopwatch.GetTimestamp();
                    double? cpu = null; string cpuStatus = "warmup"; long? rss = null; string? path = null;
                    try
                    {
                        var total = process.TotalProcessorTime.TotalSeconds;
                        if (previous.TryGetValue(id, out var prev)) cpu = Measurements.Cpu(prev.Cpu, total, Stopwatch.GetElapsedTime(prev.Stamp, now).TotalSeconds, cpuCount);
                        previous[id] = (total, now); cpuStatus = cpu is null ? "warmup" : "ok";
                    }
                    catch (System.ComponentModel.Win32Exception) { cpuStatus = "permission_denied"; }
                    try { rss = process.WorkingSet64; path = process.MainModule?.FileName; }
                    catch (System.ComponentModel.Win32Exception) { }
                    (string? Hash, string Trust, string? Signer) identity = (null, "not_inspected", null);
                    // Bounded expensive work; Inspector caches unchanged files. Rotate order using prior cache misses.
                    if (path is not null)
                    {
                        var cached = inspector is ExecutableInspector e && e.IsCached(path);
                        if (cached || inspections < 8) { identity = inspector.Inspect(path); if (!cached) inspections++; }
                    }
                    var meta = metadata.GetValueOrDefault(process.Id);
                    if (meta?.StartedAt is not DateTimeOffset ms || Math.Abs((ms - start).TotalMilliseconds) > 1) meta = null;
                    list.Add(new(id, process.Id, meta?.Ppid, start, path, identity.Hash, "authenticode", identity.Trust, identity.Signer,
                        new(cpu, "percent_host_capacity", cpuStatus, "process_user_kernel_times"), rss, meta?.ArgumentFeatures ?? [],
                        path is null ? "permission_denied" : meta is null ? "partial_metadata" : "ok"));
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                {
                    // Without creation time no stable process identity can be asserted. Keep coverage, never invent a PID-only identity.
                }
            }
        }
        foreach (var id in previous.Keys.Where(id => !alive.Contains(id)).ToArray()) previous.Remove(id);
        return list.ToArray();
    }
    public static ProcessMetadata[] ReadMetadata()
    {
        var options = new System.Management.EnumerationOptions { ReturnImmediately = false, Timeout = TimeSpan.FromSeconds(5) };
        using var search = new ManagementObjectSearcher(new ManagementScope("root\\cimv2"), new ObjectQuery("SELECT ProcessId,ParentProcessId,CreationDate,CommandLine FROM Win32_Process"), options);
        using var objects = search.Get(); var output = new List<ProcessMetadata>();
        foreach (ManagementObject item in objects)
        {
            using (item)
            {
                var date = item["CreationDate"] as string;
                if (date is null) continue;
                var started = new DateTimeOffset(ManagementDateTimeConverter.ToDateTime(date).ToUniversalTime());
                output.Add(new(Convert.ToInt32(item["ProcessId"]), started, Convert.ToInt32(item["ParentProcessId"]), Privacy.ArgumentFeatures(item["CommandLine"] as string)));
            }
        }
        return output.ToArray();
    }
}
