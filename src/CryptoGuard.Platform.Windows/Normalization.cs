using CryptoGuard.Contracts;
using Legacy = CryptoGuard.Compatibility.Windows.Contracts;

namespace CryptoGuard.Platform.Windows;

// The OS collectors keep their tested native interfaces. Only this boundary knows their legacy records.
public static class Normalization
{
    public static Metric Metric(Legacy.Metric m) => new(m.Value, m.Unit, m.Status == "warmup" ? "warm_up" : m.Status, m.Source);
    public static AgentConfig Policy(Legacy.AgentConfig c, string directory) => new()
    {
        StateDirectory = directory, SampleSeconds = c.SampleSeconds, NetworkSeconds = c.NetworkSeconds,
        SensorSeconds = c.HardwareSeconds, InventorySeconds = c.InventorySeconds, ExportSeconds = c.SummarySeconds,
        PersistenceSeconds = c.PersistenceSeconds, SpoolLimitBytes = c.SpoolMaxBytes, RiskReserveBytes = c.RiskReserveBytes,
        RetentionHours = checked(c.RetentionDays * 24),
        Iocs = c.Iocs.Select(i => new Ioc { Type = i.Kind, Value = i.Value, Source = i.Source, Confidence = i.Confidence == "high" ? 1 : 0, ExpiresAt = i.ExpiresAt }).ToList(),
        AuthorizedWorkloads = c.Authorization.Where(a => a.Decision == "allowed").Select(a => new AuthorizedWorkload { Sha256 = a.Sha256, ExePath = a.ExePath, Publisher = a.Signer, PolicyId = "local-approved-workload" }).ToList(),
        MlShadowEnabled = c.MlShadowEnabled, MlModelPath = c.MlModelPath, MlModelSha256 = c.MlModelSha256
    };
    public static Snapshot Snapshot(Legacy.Snapshot s, DateTimeOffset observedAt, double monotonicMs, double durationMs)
    {
        var h = s.Host;
        var result = new Snapshot
        {
            ObservedAt = observedAt, MonotonicMs = monotonicMs, SampleDurationMs = durationMs,
            Host = new() { Platform = "windows", OsVersion = h.OsVersion, Architecture = h.Architecture, BootId = h.BootId, Hostname = h.Hostname,
                LogicalCpuCount = h.LogicalCpuCount, CpuPercent = Metric(h.CpuPercentHostCapacity), MemoryUsedPercent = Metric(h.MemoryUsedPercent),
                MemoryTotalBytes = h.TotalMemoryBytes is long total ? Contracts.Metric.Ok(total,"bytes","GlobalMemoryStatusEx") : Contracts.Metric.Missing("bytes","GlobalMemoryStatusEx"),
                NetworkReceiveBytesPerSecond = Metric(h.NetworkReceiveBytesPerSecond), NetworkTransmitBytesPerSecond = Metric(h.NetworkSendBytesPerSecond),
                IdleSeconds = new(s.Idle.IdleSeconds,"seconds",s.Idle.Status,s.Idle.Source) },
            Coverage = s.Coverage.Select(c => new Capability(c.Name,c.Status,c.Source,c.Detail)).ToList(),
            Errors = s.Health.CollectorErrors.Select(c => new CollectorError(c.Split(':')[0],"temporarily_unavailable",c)).ToList(),
            Sensors = s.Sensors.Select(x => new Sensor(x.Id,x.DeviceId,x.Name,x.Type.ToLowerInvariant(),new(x.Value,x.Unit,x.Status,x.Source))).ToList(),
            Devices = s.Sensors.GroupBy(x => x.DeviceId).Select(g => new Device(g.Key,"unknown",g.First().DeviceName,g.First().Source,"device")).ToList()
        };
        foreach(var p in s.Processes)
        {
            var flags=p.ArgumentFeatures.ToHashSet(StringComparer.Ordinal);
            result.Processes.Add(new() { ProcessInstanceId=p.ProcessInstanceId, Pid=p.Pid, Ppid=p.Ppid, StartedAt=p.StartedAt,
                ExePath=p.ExePath, ExeStatus=p.Status, Sha256=p.Sha256, HashStatus=p.Sha256 is null ? "temporarily_unavailable" : "ok",
                RedactedArguments=p.ArgumentFeatures, MiningArgumentCombination=flags.Contains("stratum_protocol") && (flags.Contains("mining_algorithm") || flags.Contains("wallet_option")),
                UnusualWritableLocation=SuspiciousPath(p.ExePath), Browser=IsBrowser(p.ExePath), CpuPercentHostCapacity=Metric(p.CpuPercentHostCapacity),
                RssBytes=p.RssBytes is long rss ? Contracts.Metric.Ok(rss,"bytes","process_working_set") : Contracts.Metric.Missing("bytes","process_working_set","permission_denied"),
                FileTrust=new("authenticode",p.TrustStatus,null,null) { Publisher=p.Signer, ExecutableFormat="PE" } });
        }
        result.Connections=s.Connections.Select(c => new Connection("host",c.Family.ToLowerInvariant(),c.LocalAddress,c.LocalPort,c.RemoteAddress,c.RemotePort,c.State.ToLowerInvariant(),"",c.ProcessInstanceId,c.ProcessInstanceId is null ? "temporarily_unavailable" : "ok")).ToList();
        foreach(var item in s.Persistence)
        {
            var owners=result.Processes.Where(p => p.ExePath is not null && string.Equals(p.ExePath,item.ExecutablePath,StringComparison.OrdinalIgnoreCase)).Select(p => p.ProcessInstanceId).ToArray();
            if(owners.Length==0)result.Persistence.Add(new(item.Kind,item.Location,item.ExecutablePath,null,item.Status=="observed"?"ok":item.Status));
            foreach(var owner in owners)result.Persistence.Add(new(item.Kind,item.Location,item.ExecutablePath,owner,item.Status=="observed"?"ok":item.Status));
        }
        return result;
    }
    public static TelemetryEvent Event(Legacy.EventEnvelope old)
    {
      var result = new TelemetryEvent {
        EventUid=old.EventUid, EventType=old.EventType=="diagnostic"?"health":old.EventType, AgentId=old.AgentId, AgentVersion=old.AgentVersion,
        Sequence=old.Sequence, ObservedAt=old.ObservedAt, SampleDurationMs=old.SampleDurationMs,
        Snapshot=Snapshot(old.Data,old.ObservedAt,0,old.SampleDurationMs),
        Risk=old.Data.Risks.Select(r => new RiskResult { ProcessInstanceId=r.ProcessInstanceId,Score=r.Score,Severity=r.Severity,Assessment=r.Assessment,
            Authorization=r.Authorization,RulesetVersion=r.RulesetVersion,ModelVersion=r.ModelVersion,
            Evidence=r.Evidence.Select(e=>new Evidence(e.Group,e.Weight,e.Code,"legacy_windows") { ProcessInstanceId=r.ProcessInstanceId }).ToList() }).ToList(),
        Health=new() { SpoolBytes=old.Data.Health.SpoolBytes,DroppedEvents=old.Data.Health.DroppedEvents,CollectionDurationMs=old.Data.Health.CollectionDurationMs,RecoveredPartialWrites=old.Data.Health.RecoveredIncompleteWrites }
      };
      foreach(var alert in old.Data.Alerts)
      {
          var risk=result.Risk.FirstOrDefault(r=>r.ProcessInstanceId==alert.ProcessInstanceId);
          if(risk is null)
          {
              var r=alert.Risk;
              risk=new() { ProcessInstanceId=r.ProcessInstanceId,Score=r.Score,Severity=r.Severity,Assessment=r.Assessment,Authorization=r.Authorization,RulesetVersion=r.RulesetVersion,ModelVersion=r.ModelVersion,
                  Evidence=r.Evidence.Select(e=>new Evidence(e.Group,e.Weight,e.Code,"legacy_windows"){ProcessInstanceId=r.ProcessInstanceId}).ToList() };
              result.Risk.Add(risk);
          }
          risk.AlertId=alert.AlertId;risk.Lifecycle=alert.State;risk.FirstSeen=alert.FirstSeen;risk.LastSeen=alert.LastSeen;risk.LifecycleChanged=true;
      }
      return result;
    }
    public static TelemetrySummary Summary(Legacy.TelemetrySummary s) => new(s.StartedAt,s.EndedAt,s.DurationMs,s.SampleCount,
        SummaryMetric(s.HostCpuPercent),SummaryMetric(s.HostMemoryUsedPercent),SummaryMetric(s.ReceiveBytesPerSecond),SummaryMetric(s.SendBytesPerSecond),
        s.Processes.Select(p=>new ProcessSummary(p.ProcessInstanceId,SummaryMetric(p.CpuPercentHostCapacity),SummaryMetric(p.RssBytes))).ToArray());
    private static SummaryMetric SummaryMetric(Legacy.SummaryMetric s) => new(s.Minimum,s.Maximum,s.Mean,s.Unit,s.ValidSamples,s.TotalSamples,s.CoveredSeconds);
    private static bool SuspiciousPath(string? path) => path is not null && new[]{"\\temp\\","\\appdata\\local\\temp\\","\\users\\public\\"}.Any(x => path.Contains(x,StringComparison.OrdinalIgnoreCase));
    private static bool IsBrowser(string? path) => new[]{"chrome.exe","msedge.exe","firefox.exe","brave.exe","opera.exe"}.Contains(Path.GetFileName(path),StringComparer.OrdinalIgnoreCase);
}
