using CryptoGuard.Contracts;

namespace CryptoGuard.Core;

public static class CapabilitySummary
{
    public static void Enrich(Snapshot sample,AgentConfig config)
    {
        void Set(string name,string status,string source,string detail)
        { sample.Coverage.RemoveAll(c=>c.Name==name);sample.Coverage.Add(new(name,status,source,detail)); }
        Set("cpu",sample.Host.CpuPercent.Status,sample.Host.CpuPercent.Source,"Host capacity; a first sample requires a second counter observation.");
        Set("ram",sample.Host.MemoryUsedPercent.Status,sample.Host.MemoryUsedPercent.Source,"Physical RAM; unknown remains null.");
        if(!sample.Coverage.Any(c=>c.Name=="process"))Set("process",sample.Errors.Any(e=>e.Collector=="process" && e.Status!="ok")?"partial":"ok","proc.snapshot","Stable creation-time identity; inaccessible processes reduce coverage.");
        if(!sample.Coverage.Any(c=>c.Name=="network"))Set("network",sample.Errors.Any(e=>e.Collector=="network" && e.Status!="ok")?"partial":"ok","proc.tcp_owner","IPv4/IPv6 namespace and inode attribution.");
        var gpu=sample.Sensors.Where(s=>s.Reading.Unit=="percent" && (s.Type is "utilization" || s.DeviceId.Contains("gpu",StringComparison.OrdinalIgnoreCase))).ToArray();
        Set("gpu",gpu.Any(s=>s.Reading.Status=="ok")?"ok":gpu.Any(s=>s.Reading.Status=="permission_denied")?"permission_denied":"unsupported","device_hardware","No supported GPU reading is a normal result; no process attribution is inferred.");
        var temperatures=sample.Sensors.Where(s=>s.Type.Equals("temperature",StringComparison.OrdinalIgnoreCase)).ToArray();
        Set("temperature",temperatures.Any(s=>s.Reading.Status=="ok")?"ok":temperatures.Any(s=>s.Reading.Status=="permission_denied")?"permission_denied":"unsupported","labeled_sensors","A generic thermal zone is never automatically a CPU sensor.");
        Set("idle",sample.Host.IdleSeconds.Status,sample.Host.IdleSeconds.Source,"Only supported interactive sessions; headless and Session 0 may be unavailable.");
        Set("trust","partial",sample.Host.Platform=="windows"?"authenticode":"package_origin","Bounded cached read-only inspection; local trust metadata is not workload authorization.");
        Set("spool",Directory.Exists(config.StateDirectory)?"available":"not_initialized","durable_local_queue","Write/ACK health is reported separately; this capability does not claim a disk write succeeded.");
        Set("risk_engine","ok",ReleaseVersions.Ruleset,"Heuristic decisions; security and resource scores are separate.");
        Set("ml_shadow",config.MlShadowEnabled?"configured":"disabled",ReleaseVersions.Features,"Optional shadow predictions cannot change heuristic decisions; model load status accompanies each prediction.");
    }
}
