using CryptoGuard.Contracts;

namespace CryptoGuard.ML;

public sealed record FeatureValue(double? Value,string Status,string Unit);
public static class Features
{
    public static readonly string[] Names = ["process_cpu_percent_host_capacity","process_rss_bytes","host_cpu_percent_host_capacity","host_memory_used_percent","logical_cpu_count",
        "network_tx_bytes_per_sec","network_rx_bytes_per_sec","device_gpu_percent","cpu_temperature_celsius","gpu_temperature_celsius","process_count","high_cpu_process_count",
        "owned_tcp_connections","established_tcp_connections","persistence_present","mining_argument_combination","suspicious_path","executable_trusted","process_age_seconds",
        "host_memory_total_bytes","sustained_compute_seconds","process_gpu_percent"];
    public static Dictionary<string,FeatureValue> Extract(Snapshot s,ProcessSample p,double? sustainedSeconds=null)
    {
        var f=new Dictionary<string,FeatureValue>(StringComparer.Ordinal);
        void Add(string name,double? value,string unit,string status="ok") => f[name]=new(value is double v && double.IsFinite(v) ? v : null,value is double n && double.IsFinite(n) ? "ok" : status=="ok"?"temporarily_unavailable":status,unit);
        void Metric(string name,Metric m) => Add(name,m.Status=="ok"?m.Value:null,m.Unit,m.Status);
        bool Covered(string name)=>s.Coverage.Any(c=>c.Name==name && c.Status=="ok");
        Metric("process_cpu_percent_host_capacity",p.CpuPercentHostCapacity);Metric("process_rss_bytes",p.RssBytes);
        Metric("host_cpu_percent_host_capacity",s.Host.CpuPercent);Metric("host_memory_used_percent",s.Host.MemoryUsedPercent);
        Add("logical_cpu_count",s.Host.LogicalCpuCount,"count");
        Metric("network_tx_bytes_per_sec",s.Host.NetworkTransmitBytesPerSecond);Metric("network_rx_bytes_per_sec",s.Host.NetworkReceiveBytesPerSecond);
        var devices=s.Devices.ToDictionary(d=>d.DeviceId,d=>d.Name,StringComparer.Ordinal);
        bool IsGpu(Sensor sensor)=>sensor.Reading.Source=="nvml" || sensor.Reading.Source=="drm.sysfs" || devices.GetValueOrDefault(sensor.DeviceId,"").Contains("GPU",StringComparison.OrdinalIgnoreCase) || sensor.DeviceId.Contains("gpu",StringComparison.OrdinalIgnoreCase);
        double? Max(IEnumerable<Sensor> sensors)=>sensors.Where(x=>x.Reading.Status=="ok").Select(x=>x.Reading.Value).DefaultIfEmpty(null).Max();
        Add("device_gpu_percent",Max(s.Sensors.Where(x=>IsGpu(x) && x.Type is "load" or "utilization" && x.Reading.Unit=="percent")),"percent","unsupported");
        Add("cpu_temperature_celsius",Max(s.Sensors.Where(x=>x.Type=="temperature" && (x.DeviceId.Contains("cpu",StringComparison.OrdinalIgnoreCase) || devices.GetValueOrDefault(x.DeviceId,"") is "coretemp" or "k10temp"))),"celsius","unsupported");
        Add("gpu_temperature_celsius",Max(s.Sensors.Where(x=>IsGpu(x) && x.Type=="temperature")),"celsius","unsupported");
        Add("process_count",Covered("process")?s.Processes.Count:null,"count");
        Add("high_cpu_process_count",Covered("process")?s.Processes.Count(x=>x.CpuPercentHostCapacity is { Status:"ok",Value:>=15 }):null,"count");
        var connections=s.Connections.Where(c=>c.ProcessInstanceId==p.ProcessInstanceId && c.OwnerStatus=="ok").ToArray();
        Add("owned_tcp_connections",Covered("network")||Covered("tcp")?connections.Length:null,"count");
        Add("established_tcp_connections",Covered("network")||Covered("tcp")?connections.Count(c=>c.State=="established"):null,"count");
        bool persistence=s.Persistence.Any(x=>x.ProcessInstanceId==p.ProcessInstanceId && x.Status=="ok");
        Add("persistence_present",persistence?1:Covered("persistence")?0:null,"boolean");
        Add("mining_argument_combination",p.MiningArgumentCombination?1:p.ExeStatus is "ok"?0:null,"boolean");
        Add("suspicious_path",p.ExePath is null?null:p.UnusualWritableLocation?1:0,"boolean");
        double? trusted=p.FileTrust.TrustType=="authenticode" ? p.FileTrust.Status is "trusted_offline" or "trusted_catalog_offline" ? 1 : p.FileTrust.Status is "unsigned" or "invalid" ? 0 : null : p.FileTrust.Integrity=="matches_local_package_metadata"?1:p.FileTrust.Integrity is "mismatch" or "not_owned_by_package"?0:null;
        Add("executable_trusted",trusted,"boolean");
        Add("process_age_seconds",p.StartedAt is DateTimeOffset started && s.ObservedAt>=started?(s.ObservedAt-started).TotalSeconds:null,"seconds");
        Metric("host_memory_total_bytes",s.Host.MemoryTotalBytes);
        Add("sustained_compute_seconds",sustainedSeconds,"seconds");Metric("process_gpu_percent",p.GpuPercent);
        return f;
    }
}
