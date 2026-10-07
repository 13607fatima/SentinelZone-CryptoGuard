using System.Text.Json;
using System.Text.Json.Nodes;
using CryptoGuard.Agent.Windows;
using CryptoGuard.Contracts;
using CryptoGuard.Core;
using CryptoGuard.Platform.Windows;
using Old=CryptoGuard.Compatibility.Windows.Contracts;
using OldCore=CryptoGuard.Compatibility.Windows.Core;

int passed=0,failed=0;var results=new JsonArray();
void Check(bool v){if(!v)throw new InvalidOperationException("assertion");}
void Test(string name,Action body){try{body();passed++;Console.WriteLine("PASS "+name);results.Add(new JsonObject { ["name"]=name,["status"]="PASS" });}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+" "+e.GetType().Name);results.Add(new JsonObject { ["name"]=name,["status"]="FAIL",["error_type"]=e.GetType().Name });}}
string Temp(){var p=Path.Combine(Path.GetTempPath(),"cryptoguard-migration-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(p);return p;}
Test("windows_partial_ml_config_preserves_defaults",()=> { var path=Path.Combine(Temp(),"config.json");File.WriteAllText(path,"{\"ml_shadow_enabled\":true,\"ml_model_path\":\"research.json\"}");var c=ConfigurationLoader.Load(path);c.Validate();Configuration.Validate(Normalization.Policy(c,Temp()),false);Check(c.MlShadowEnabled && c.SampleSeconds==5 && c.Iocs.Length==0 && c.Authorization.Length==0 && c.SpoolMaxBytes>0); });
Test("windows_unknown_config_property_rejected",()=> { var path=Path.Combine(Temp(),"config.json");File.WriteAllText(path,"{\"unknown_setting\":1}");bool rejected=false;try { ConfigurationLoader.Load(path); }catch(JsonException){rejected=true;}Check(rejected); });
Old.EventEnvelope Legacy(string id,long sequence)
{
    var metric=new Old.Metric(20,"percent","ok","fixture");
    var host=new Old.HostSample("windows","fixture","x64","boot","redacted",4,metric,metric,8000000000,metric,metric);
    var snapshot=new Old.Snapshot(host,[],[],[],[],new(null,null,"unsupported","session_0"),[],[],[],new(0,0,0,0,[]));
    return new("1.1","telemetry",Guid.NewGuid().ToString(),id,"0.22.0-alpha.3",sequence,DateTimeOffset.UtcNow,5000,snapshot);
}
Test("windows_identity_sequence_and_spool_migration",()=>
{
    var path=Temp();string id;long sequence;
    using(var state=new OldCore.StateStore(path)){id=state.AgentId;sequence=state.ReserveSequence();var oldSpool=new OldCore.FileSpool(Path.Combine(path,"spool"),new());Check(oldSpool.Append(Legacy(id,sequence),false));}
    var original=File.ReadAllBytes(Path.Combine(path,"identity.json"));
    StateMigration.Prepare(path);
    using var spool=new DurableSpool(new() { StateDirectory=path });
    Check(spool.State.AgentId==id && spool.NextSequence()>sequence && StateMigration.ImportSpool(path,spool));
    var imported=spool.ReadAll().Single();Check(imported.Sequence==sequence && imported.AgentId==id && imported.SchemaVersion==ReleaseVersions.Schema);
    Check(File.ReadAllBytes(Path.Combine(path,"identity.json.pre-0.22.2")).SequenceEqual(original));
    Check(Directory.GetFiles(Path.Combine(path,"spool"),"*.json").Length==1);
    Check(StateMigration.ImportSpool(path,spool) && spool.ReadAll().Count()==1);
});
foreach(var lifecycle in new[]{"confirmed","resolved"})Test("windows_"+lifecycle+"_alert_and_pending_migration",()=>
{
    var path=Temp();string id;using(var state=new OldCore.StateStore(path)){id=state.AgentId;state.ReserveSequence();}
    var risk=new Old.RiskResult("instance",67,"high","mining_activity_suspected","unknown","old-rules",null,[],"partial");
    var alertId=Guid.NewGuid().ToString();var alert=new Old.Alert(alertId,"instance",lifecycle,DateTimeOffset.UtcNow.AddMinutes(-3),DateTimeOffset.UtcNow,risk);
    var pending=Legacy(id,1) with { EventType="risk" };
    pending=pending with { Data=pending.Data with { Alerts=[alert],Risks=[risk] } };
    var old=new Old.DetectionCheckpoint("1.1","boot","old-rules",200,[],[new(alert,100,null,200)]) { PendingEvent=pending };
    var oldBytes=JsonSerializer.SerializeToUtf8Bytes(old,Old.WireJson.Default.DetectionCheckpoint);File.WriteAllBytes(Path.Combine(path,"detection-state.json"),oldBytes);
    StateMigration.Prepare(path);using var spool=new DurableSpool(new() { StateDirectory=path });var checkpoint=new RiskCheckpointStore(path);
    Check(checkpoint.RestoreStates().Single().AlertId==alertId && checkpoint.RestoreStates().Single().Lifecycle==lifecycle && checkpoint.HasPendingEvent);
    Check(checkpoint.FlushPending(spool) && spool.ReadAll().Single().EventUid==pending.EventUid);
    Check(spool.ReadAll().Single().Risk.Single().AlertId==alertId && spool.ReadAll().Single().Risk.Single().Lifecycle==lifecycle);
    Check(File.ReadAllBytes(Path.Combine(path,"detection-state.json")).SequenceEqual(oldBytes));
});
Test("migration_corrupt_state_keeps_original",()=>
{
    var path=Temp();var bad=System.Text.Encoding.UTF8.GetBytes("{invalid-json");File.WriteAllBytes(Path.Combine(path,"identity.json"),bad);
    bool rejected=false;try{StateMigration.Prepare(path);}catch(JsonException){rejected=true;}Check(rejected && File.ReadAllBytes(Path.Combine(path,"identity.json")).SequenceEqual(bad));
});
Test("migration_full_spool_retains_original_pending",()=>
{
    var path=Temp();string id;using(var state=new OldCore.StateStore(path)){id=state.AgentId;var oldSpool=new OldCore.FileSpool(Path.Combine(path,"spool"),new());Check(oldSpool.Append(Legacy(id,state.ReserveSequence()),false));}
    StateMigration.Prepare(path);using var spool=new DurableSpool(new() { StateDirectory=path,SpoolLimitBytes=100,RiskReserveBytes=0 });
    Check(!StateMigration.ImportSpool(path,spool) && Directory.GetFiles(Path.Combine(path,"spool"),"*.json").Length==1 && !File.Exists(Path.Combine(path,"legacy-spool-import.complete")));
});
Test("windows_normalized_unknown_metrics",()=> { var old=Legacy(Guid.NewGuid().ToString(),1);var normalized=Normalization.Event(old);Check(normalized.Snapshot!.Host.IdleSeconds.Value is null);LocalCommands.Validate(normalized); });
if(args.Length>0){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);File.WriteAllText(args[0],new JsonObject { ["passed"]=passed,["failed"]=failed,["skipped"]=0,["tests"]=results }.ToJsonString(new JsonSerializerOptions { WriteIndented=true }));}
Console.WriteLine($"RESULT passed={passed} failed={failed}");return failed==0?0:1;
