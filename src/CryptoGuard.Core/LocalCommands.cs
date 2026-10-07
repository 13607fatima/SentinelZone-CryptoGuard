using System.Security.Cryptography;
using System.Text.Json;
using CryptoGuard.Contracts;

namespace CryptoGuard.Core;

public static class LocalCommands
{
    public static void Export(string directory,TextWriter output)
    {
        var queue=Path.Combine(directory,"spool");
        if(!Directory.Exists(queue))return;
        foreach(var path in Directory.EnumerateFiles(queue,"*.event").Order(StringComparer.Ordinal).ToArray())
        {
            byte[] bytes;
            try
            {
                if(new FileInfo(path).Length>16*1024*1024)throw new InvalidDataException("event_size");
                bytes=File.ReadAllBytes(path);
            }
            catch(FileNotFoundException){continue;}
            var hash=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if(!Path.GetFileName(path).Contains("-"+hash+"-",StringComparison.Ordinal))throw new InvalidDataException("spool_checksum");
            var item=JsonSerializer.Deserialize(bytes,ContractJson.Default.TelemetryEvent) ?? throw new InvalidDataException("event_json");
            Validate(item);
            output.WriteLine(JsonSerializer.Serialize(item,ContractJson.Default.TelemetryEvent));
        }
    }
    public static void Status(string directory,TextWriter output)
    {
        var path=Path.Combine(directory,"health.json");
        if(!File.Exists(path)) { output.WriteLine("{\"status\":\"unavailable\",\"reason\":\"no_health_snapshot\"}");return; }
        if(new FileInfo(path).Length>16*1024*1024)throw new InvalidDataException("health_size");
        var item=JsonSerializer.Deserialize(File.ReadAllBytes(path),ContractJson.Default.TelemetryEvent) ?? throw new InvalidDataException("health_json");
        output.WriteLine(JsonSerializer.Serialize(item,ContractJson.Default.TelemetryEvent));
    }
    public static void Validate(TelemetryEvent item)
    {
        if(item.SchemaVersion is not (ReleaseVersions.Schema or "1.0.0") || !Guid.TryParse(item.EventUid,out _) || !Guid.TryParse(item.AgentId,out _) || item.Sequence<1 ||
            item.ObservedAt.Offset!=TimeSpan.Zero || item.EventType is not ("telemetry" or "inventory" or "risk" or "health") || !double.IsFinite(item.SampleDurationMs) || item.SampleDurationMs<0)
            throw new InvalidDataException("event_envelope");
        if(item.Snapshot is not Snapshot s)return;
        var metrics=new[]{s.Host.CpuPercent,s.Host.MemoryUsedPercent,s.Host.MemoryTotalBytes,s.Host.NetworkReceiveBytesPerSecond,s.Host.NetworkTransmitBytesPerSecond,s.Host.IdleSeconds}
            .Concat(s.Processes.SelectMany(p=>new[]{p.CpuPercentHostCapacity,p.RssBytes,p.GpuPercent})).Concat(s.Sensors.Select(x=>x.Reading));
        foreach(var m in metrics)if(m.Status=="ok" ? m.Value is null || !double.IsFinite(m.Value.Value) : m.Value is not null)throw new InvalidDataException("metric_status");
        foreach(var p in s.Processes)if(string.IsNullOrWhiteSpace(p.ProcessInstanceId) || p.Pid<0)throw new InvalidDataException("process_identity");
    }
}
