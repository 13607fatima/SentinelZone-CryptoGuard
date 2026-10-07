using System.Text.Json;
using CryptoGuard.Contracts;
using CryptoGuard.Core;
using CryptoGuard.Platform.Windows;
using Legacy = CryptoGuard.Compatibility.Windows.Contracts;

namespace CryptoGuard.Agent.Windows;

internal static class StateMigration
{
    // Caller holds agent.lock across migration and the entire new agent run.
    public static void Prepare(string directory)
    {
        var identity=Path.Combine(directory,"identity.json");
        if(File.Exists(identity))
        {
            using var doc=JsonDocument.Parse(File.ReadAllBytes(identity));
            if(doc.RootElement.TryGetProperty("next_sequence",out _))
            {
                var old=JsonSerializer.Deserialize(File.ReadAllBytes(identity),Legacy.WireJson.Default.AgentIdentity) ?? throw new InvalidDataException("legacy_identity");
                if(!Guid.TryParse(old.AgentId,out _) || old.NextSequence<1)throw new InvalidDataException("legacy_identity");
                Backup(identity);
                long.TryParse(File.Exists(Path.Combine(directory,"spool","losses.txt")) ? File.ReadAllText(Path.Combine(directory,"spool","losses.txt")) : "0",out var losses);
                AtomicFile.Write(identity,JsonSerializer.SerializeToUtf8Bytes(new DurableState { AgentId=old.AgentId,Sequence=old.NextSequence-1,DroppedEvents=losses },ContractJson.Default.DurableState));
            }
        }
        var checkpoint=Path.Combine(directory,"risk-checkpoint.json");
        var legacy=Path.Combine(directory,"detection-state.json");
        if(!File.Exists(checkpoint) && File.Exists(legacy))
        {
            var old=new CryptoGuard.Compatibility.Windows.Core.DetectionJournal(legacy).Current;
            if(old is null)return;
            var states=old.Alerts.Select(a => new RiskState { InstanceId=a.Alert.ProcessInstanceId,BootId=old.BootId,
                LastMs=a.Last*1000,SuspectSinceMs=a.SuspectedSince*1000,ClearSinceMs=a.ClearSince*1000,Lifecycle=a.Alert.State,
                AlertId=a.Alert.AlertId,FirstSeen=a.Alert.FirstSeen,LastSeen=a.Alert.LastSeen }).ToList();
            var migrated=new RiskCheckpoint { States=states,PendingEvent=old.PendingEvent is null ? null : Normalization.Event(old.PendingEvent) };
            Backup(legacy);
            AtomicFile.Write(checkpoint,JsonSerializer.SerializeToUtf8Bytes(migrated,ContractJson.Default.RiskCheckpoint));
        }
    }
    public static bool ImportSpool(string directory,DurableSpool spool)
    {
        var marker=Path.Combine(directory,"legacy-spool-import.complete");
        if(File.Exists(marker))return true;
        foreach(var path in Directory.EnumerateFiles(Path.Combine(directory,"spool"),"*.json").Order(StringComparer.Ordinal))
        {
            if(new FileInfo(path).Length>16*1024*1024)throw new InvalidDataException("legacy_event_size");
            var old=JsonSerializer.Deserialize(File.ReadAllBytes(path),Legacy.WireJson.Default.EventEnvelope) ?? throw new InvalidDataException("legacy_event");
            if(!spool.Append(Normalization.Event(old)))return false;
            // Retain the original immutable JSON for rollback/inspection; migration never deletes it.
        }
        AtomicFile.Write(marker,System.Text.Encoding.UTF8.GetBytes("0.22.2-rc.1: original JSON files retained; explicit archive cleanup only\n"));
        return true;
    }
    private static void Backup(string file)
    {
        string backup=file+".pre-0.22.2";
        if(!File.Exists(backup))File.Copy(file,backup,false);
    }
}
