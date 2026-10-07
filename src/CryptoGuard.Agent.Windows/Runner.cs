using System.Diagnostics;
using System.Text.Json;
using CryptoGuard.Compatibility.Windows.Contracts;
using CryptoGuard.Compatibility.Windows.Core;
using CryptoGuard.Platform.Windows;
using Canonical = CryptoGuard.Contracts;
using Shared = CryptoGuard.Core;

namespace CryptoGuard.Agent.Windows;

internal sealed class Runner(string data, AgentConfig config, IIdleCollector? sessionIdle = null)
{
    public async Task<Canonical.TelemetryEvent> RunAsync(double durationSeconds, bool capture, bool doctor, CancellationToken ct)
    {
        Directory.CreateDirectory(data);
        using var lease = new FileStream(Path.Combine(data,"agent.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        StateMigration.Prepare(data);
        var policy = Normalization.Policy(config,data);
        using var spool = new Shared.DurableSpool(policy);
        var host = new HostCollector(); var inspector = new ExecutableInspector();
        var processes = new ProcessCollector(spool.State.AgentId, host.BootId, host.CpuCount, inspector);
        var network = new NetworkCollector(); var idle = new IdleCollector(); await using var hardware = new HardwareCollector(config);
        var journal = new Shared.RiskCheckpointStore(data);
        var risk = new CryptoGuard.Risk.RuleEngine(policy,journal.RestoreStates());
        var watch = Stopwatch.StartNew();
        var bootSeconds = Environment.TickCount64/1000d;
        var (exe,prefix) = ChildCollector.SelfCommand();
        var persistenceGate=new PersistenceRefreshGate();
        var summaryWindow=new Shared.SummaryWindow();
        double lastSample = 0, lastNetwork = -1e9, lastHardware = -1e9, lastInventory = -1e9, lastPersistence = -1e9, lastExport = -1e9;
        Connection[] connections = []; PersistenceEntry[] persistence = []; Sensor[] sensors = [];
        var inventoryStatus = "temporarily_unavailable"; var persistenceStatus = "temporarily_unavailable"; var networkStatus = "temporarily_unavailable";
        Canonical.TelemetryEvent? result = null;
        do
        {
            ct.ThrowIfCancellationRequested(); var begin = watch.Elapsed.TotalSeconds; var errors = new List<string>();
            spool.PruneExpired(DateTimeOffset.UtcNow);
            if(!StateMigration.ImportSpool(data,spool) || !journal.FlushPending(spool))
            {
                Console.Error.WriteLine("risk_outbox_pending; detection_paused");
                if(doctor || durationSeconds>0 && watch.Elapsed.TotalSeconds>=durationSeconds)throw new IOException("outbox_pending");
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(5,config.SampleSeconds)),ct);continue;
            }
            if (begin - lastInventory >= Math.Min(30,config.InventorySeconds))
            {
                try { processes.SetMetadata(await ChildCollector.ReadAsync(exe, [..prefix, "internal-metadata"], WireJson.Default.ProcessMetadataArray, 8, ct)); inventoryStatus = "ok"; }
                catch (Exception ex) when (CollectorFailure(ex, ct)) { inventoryStatus = "temporarily_unavailable"; errors.Add("process_metadata:" + ex.GetType().Name); }
                lastInventory = begin;
            }
            if (persistenceGate.IsDue(begin,lastPersistence,config.PersistenceSeconds))
            {
                try { persistence = await ChildCollector.ReadAsync(exe, [..prefix, "internal-persistence"], WireJson.Default.PersistenceEntryArray, 8, ct); persistenceStatus = persistence.Any(p => p.Status != "observed") ? "partial" : "ok"; }
                catch (Exception ex) when (CollectorFailure(ex, ct)) { persistence = []; persistenceStatus = "temporarily_unavailable"; errors.Add("persistence:" + ex.GetType().Name); }
                lastPersistence = begin;
                persistenceGate.Refreshed();
            }
            var hostSample = host.Collect(); var samples = processes.Collect();
            if (begin - lastNetwork >= config.NetworkSeconds)
            {
                try { connections = network.Collect(samples.ToDictionary(p => p.Pid, p => p.ProcessInstanceId)); networkStatus = "ok"; }
                catch (Exception ex) when (CollectorFailure(ex, ct)) { connections = []; networkStatus = "temporarily_unavailable"; errors.Add("network:" + ex.GetType().Name); }
                lastNetwork = begin;
            }
            // Revalidate cached owner mappings after PID reuse or exit.
            var active = samples.Select(s => s.ProcessInstanceId).ToHashSet();
            connections = connections.Where(c => c.ProcessInstanceId is null || active.Contains(c.ProcessInstanceId)).ToArray();
            if (begin - lastHardware >= config.HardwareSeconds) { sensors = await hardware.CollectAsync(ct); lastHardware = begin; }
            var idleSample = sessionIdle?.Collect() ?? idle.Collect();
            var coverage = new Coverage[] {
                new("host", "ok", "Win32", "Host-wide CPU/RAM and interface traffic; CPU uses all logical processors."),
                new("process", "partial", "Process + WMI", $"Creation time required; inaccessible/exited processes omitted. Metadata: {inventoryStatus}."),
                new("process_start_rate", "unsupported", "snapshot", "Short-lived processes between snapshots are not counted."),
                new("network", networkStatus, "GetExtendedTcpTable", "IPv4/IPv6 owner PID snapshots; no process byte-rate or DNS/packet attribution."),
                new("persistence", persistenceStatus, "Registry/Startup/TaskScheduler", "Current security context; unloaded user hives are outside coverage."),
                new("idle", idleSample.Status, idleSample.Source, "Interactive helper reports only; aggregation is minimum of fresh reporting sessions. Lock state not measured."),
                new("hardware", hardware.Status, "LHM/0.9.6", "Device-level telemetry; no process GPU attribution. CPU sensors opt-in."),
                new("authenticode", "partial", "WinVerifyTrust", "Offline cached trust; revocation freshness unknown. Inspection bounded per cycle."),
                new("ml", "disabled", "none", "No approved dataset/model; no ML score."),
                new("randomx", "disabled", "none", "Experimental detector is not part of this release.") };
            var elapsed = watch.Elapsed.TotalSeconds;
            var snapshot = new Snapshot(hostSample,samples,connections,persistence,sensors,idleSample,[],[],coverage,
                new(spool.SizeBytes,spool.State.DroppedEvents,spool.State.RecoveredPartialWrites,(elapsed-begin)*1000,errors.ToArray()));
            var monotonic=bootSeconds+elapsed;
            var now = DateTimeOffset.UtcNow;
            var sampleDuration=lastSample==0?0:elapsed-lastSample;
            var normalized=Normalization.Snapshot(snapshot,now,monotonic*1000,sampleDuration*1000);
            Shared.CapabilitySummary.Enrich(normalized,policy);
            var risks=risk.Evaluate(normalized);
            persistenceGate.Observe(risks.Select(r=>new RiskResult(r.ProcessInstanceId,r.Score,r.Severity,r.Assessment,r.Authorization,r.RulesetVersion,r.ModelVersion,[],"normalized")).ToArray());
            summaryWindow.Add(normalized,now,sampleDuration);
            var summaryDue=elapsed-lastExport>=config.SummarySeconds;
            if(summaryDue)normalized.Summary=summaryWindow.Build();
            var changes=risks.Where(r=>r.LifecycleChanged).ToList();
            result = new() { EventType=changes.Count>0?"risk":doctor?"health":"telemetry",AgentId=spool.State.AgentId,
                Sequence=spool.NextSequence(),ObservedAt=now,SampleDurationMs=sampleDuration*1000,Snapshot=normalized,Risk=risks,
                Health=new() { SpoolBytes=spool.SizeBytes,DroppedEvents=spool.State.DroppedEvents,RecoveredPartialWrites=spool.State.RecoveredPartialWrites,
                    CollectionDurationMs=(elapsed-begin)*1000,CollectorErrors=normalized.Errors,
                    CollectorLatency=[new("sample",(elapsed-begin)*1000,"ok")],SensorAvailability=normalized.Coverage.Where(c=>c.Name is "hardware" or "idle").ToList() } };
            lastSample = elapsed;
            bool stored;
            if(changes.Count>0)
            {
                stored=journal.Commit(risk.ExportState(),result,spool);
                if(!stored)Console.Error.WriteLine("risk_outbox_pending; detection_paused_until_spool_has_space");
            }
            else
            {
                stored=!(capture || doctor || summaryDue) || spool.Append(result);
                journal.Commit(risk.ExportState(),null,spool);
                if(!stored)Console.Error.WriteLine("spool_limit_or_write_failure; dropped_events="+spool.State.DroppedEvents);
            }
            if(summaryDue && (stored || changes.Count>0)){summaryWindow=new Shared.SummaryWindow();lastExport=elapsed;}
            WriteHealth(result);
            if (doctor || durationSeconds > 0 && elapsed >= durationSeconds) break;
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(.05, config.SampleSeconds - (watch.Elapsed.TotalSeconds-begin))),ct);
        } while (true);
        return result!;
    }
    private void WriteHealth(Canonical.TelemetryEvent item)
    {
        var snapshot=item.Snapshot!;
        var health=new Canonical.TelemetryEvent { EventType="health",EventUid=item.EventUid,AgentId=item.AgentId,Sequence=item.Sequence,
            ObservedAt=item.ObservedAt,SampleDurationMs=item.SampleDurationMs,Health=item.Health,
            Snapshot=new() { ObservedAt=item.ObservedAt,MonotonicMs=snapshot.MonotonicMs,Host=snapshot.Host,Coverage=snapshot.Coverage } };
        Shared.AtomicFile.Write(Path.Combine(data,"health.json"),JsonSerializer.SerializeToUtf8Bytes(health,Canonical.ContractJson.Default.TelemetryEvent));
    }
    private static bool CollectorFailure(Exception ex, CancellationToken ct) => !ct.IsCancellationRequested && ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or JsonException or OperationCanceledException;
}
