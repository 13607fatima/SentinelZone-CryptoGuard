using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using CryptoGuard.Contracts;
using CryptoGuard.Core;
using CryptoGuard.Risk;
using CryptoGuard.ML;

var results=new JsonArray();int passed=0,failed=0;
void Check(bool ok) { if(!ok)throw new InvalidOperationException("assertion_failed"); }
void Test(string name,string area,Action run)
{
    try { run();passed++;results.Add(new JsonObject { ["name"]=name,["area"]=area,["status"]="PASS" });Console.WriteLine("PASS "+name); }
    catch(Exception ex) { failed++;results.Add(new JsonObject { ["name"]=name,["area"]=area,["status"]="FAIL",["error_type"]=ex.GetType().Name });Console.WriteLine("FAIL "+name+" "+ex.GetType().Name); }
}
var epoch=new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.Zero);
Snapshot Sample(double seconds,bool mining=false,double? cpu=99,string instance="process-a")=>new()
{
    ObservedAt=epoch.AddSeconds(seconds),MonotonicMs=seconds*1000,SampleDurationMs=seconds==0?0:5000,
    Host=new() { Platform="linux",BootId="fixture-boot",LogicalCpuCount=4,CpuPercent=Metric.Ok(99,"percent_host_capacity","fixture"),MemoryUsedPercent=Metric.Ok(60,"percent","fixture"),MemoryTotalBytes=Metric.Ok(16L*1024*1024*1024,"bytes","fixture") },
    Processes=[new() { ProcessInstanceId=instance,Pid=42,Ppid=1,StartedAt=epoch,ExePath="/tmp/worker",ExeStatus="ok",CpuPercentHostCapacity=cpu is double v?Metric.Ok(v,"percent_host_capacity","fixture"):Metric.Missing("percent_host_capacity","fixture","permission_denied"),
        RssBytes=Metric.Ok(100*1024*1024,"bytes","fixture"),MiningArgumentCombination=mining,UnusualWritableLocation=true,RedactedArguments=mining?["--algo=[redacted]","--url=[redacted]"]:[],FileTrust=new("package_origin","unsupported",null,null) }],
    Coverage=[new("process","ok","fixture","Complete controlled inventory"),new("network","ok","fixture","Controlled fixture"),new("persistence","ok","fixture","Controlled fixture")]
};
TelemetryEvent Event(Snapshot s,long seq=1)=>new() { EventUid=new Guid((int)seq,0,0,new byte[8]).ToString(),AgentId="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",Sequence=seq,ObservedAt=s.ObservedAt,SampleDurationMs=s.SampleDurationMs,Snapshot=s };
string Decision(RiskResult r)=>$"{r.SecurityRisk}|{r.ResourceImpact}|{r.Severity}|{string.Join(',',r.Reasons)}";
RuleEngine Confirmed(AgentConfig? config=null)
{
    var e=new RuleEngine(config??new());for(int t=0;t<=90;t+=5)e.Evaluate(Sample(t,true));return e;
}
string Temp()=>Path.Combine(Path.GetTempPath(),"cryptoguard-unified-test-"+Guid.NewGuid().ToString("N"));

Test("test_windows_linux_same_normalized_input_same_risk","parity",()=>
{
    var win=new RuleEngine(new());var lin=new RuleEngine(new());
    for(int t=0;t<=180;t+=5) { var a=Sample(t,t<100,t<100?80:0);var b=JsonSerializer.Deserialize(JsonSerializer.Serialize(a,ContractJson.Default.Snapshot),ContractJson.Default.Snapshot)!;b.Host.Platform="windows";b.Coverage.Add(new("gpu","unsupported","vm","no physical GPU"));Check(Decision(win.Evaluate(b).Single())==Decision(lin.Evaluate(a).Single())); }
});
Test("test_alert_survives_restart","risk",()=> { var original=Confirmed();var before=original.ExportState().Single();var saved=JsonSerializer.Serialize(original.ExportState(),ContractJson.Default.ListRiskState);var e=new RuleEngine(new(),JsonSerializer.Deserialize(saved,ContractJson.Default.ListRiskState));var r=e.Evaluate(Sample(95,true)).Single();Check(r.Lifecycle=="confirmed" && r.AlertId==before.AlertId && !r.LifecycleChanged); });
Test("test_resolved_alert_survives_restart","risk",()=> { var e=Confirmed();RiskResult? resolved=null;for(int t=95;t<=155;t+=5)resolved=e.Evaluate(Sample(t,false,0)).Single();Check(resolved!.Lifecycle=="resolved");var again=new RuleEngine(new(),e.ExportState()).Evaluate(Sample(160,false,0)).Single();Check(again.Lifecycle=="resolved" && again.AlertId==resolved.AlertId && !again.LifecycleChanged); });
Test("test_single_cpu_spike_not_confirmed","risk",()=> { var e=new RuleEngine(new());Check(e.Evaluate(Sample(0,true)).Single().Lifecycle!="confirmed");for(int t=5;t<=100;t+=5)Check(e.Evaluate(Sample(t,false,0)).Single().Lifecycle!="confirmed"); });
Test("test_pid_reuse_does_not_inherit_alert","risk",()=> { var e=Confirmed();var r=e.Evaluate(Sample(95,true,99,"replacement-process")).First(x=>x.ProcessInstanceId=="replacement-process");Check(r.AlertId is null && r.Lifecycle=="observed" && !r.Reasons.Contains("SUSTAINED_COMPUTE")); });
Test("absent_process_gap_is_not_continuous_absence","risk",()=> { var e=Confirmed();var s=Sample(10000);s.Processes.Clear();Check(e.Evaluate(s).Count==0 && e.ExportState().Single().Lifecycle=="confirmed"); });
Test("missing_cpu_does_not_resolve_confirmed_alert","risk",()=> { var e=Confirmed();for(int t=95;t<=180;t+=5)Check(e.Evaluate(Sample(t,false,null)).Single().Lifecycle=="confirmed"); });
Test("unsigned_binary_alone_remains_low","risk",()=> { var s=Sample(0,false,0);s.Processes[0].FileTrust=new("authenticode","unsigned",null,null);Check(new RuleEngine(new()).Evaluate(s).Single().Severity=="low"); });
Test("single_port_alone_remains_low","risk",()=> { var s=Sample(0,false,0);s.Connections.Add(new("host","ipv4","127.0.0.1",50000,"192.0.2.1",3333,"established","0","process-a","ok"));Check(new RuleEngine(new()).Evaluate(s).Single().SecurityRisk==0); });
Test("trusted_render_has_high_resource_and_low_security","risk",()=> { var s=Sample(0,false,99);s.Processes[0].GpuPercent=Metric.Ok(99,"percent","fixture");s.Processes[0].FileTrust=new("authenticode","valid",null,null) { Publisher="approved-render-test" };var r=new RuleEngine(new()).Evaluate(s).Single();Check(r.ResourceImpact==99 && r.Severity=="low" && r.AssessmentSource=="heuristic"); });
Test("approved_hash_path_cannot_hide_malicious_hash","risk",()=> { var c=new AgentConfig();var hash=new string('a',64);c.AuthorizedWorkloads.Add(new() { Sha256=hash,ExePath="/tmp/worker",PolicyId="test" });c.Iocs.Add(new() { Type="sha256",Value=hash,Confidence=1,Source="fixture",ExpiresAt=epoch.AddDays(1) });var s=Sample(0);s.Processes[0].Sha256=hash;Check(new RuleEngine(c).Evaluate(s).Single().Severity=="critical"); });
Test("score_is_explained_and_not_probability","contract",()=> { var r=Confirmed().Evaluate(Sample(95,true)).Single();Check(r.SecurityRisk==r.Score && r.Reasons.Contains("MINING_ARGUMENTS") && r.Evidence.All(e=>e.ProcessInstanceId=="process-a") && r.ModelVersion=="heuristic-1.0"); });
Test("sensor_unknown_stays_null","contract",()=> { var s=Sample(0);s.Sensors.Add(new("gpu","gpu","GPU load","utilization",Metric.Missing("percent","fixture")));var json=JsonSerializer.Serialize(Event(s),ContractJson.Default.TelemetryEvent);Check(json.Contains("\"value\":null") && json.Contains("\"status\":\"unsupported\""));LocalCommands.Validate(Event(s)); });
Test("non_ok_zero_is_rejected","contract",()=> { var s=Sample(0);s.Processes[0].GpuPercent=new(0,"percent","unsupported","fixture");bool rejected=false;try { LocalCommands.Validate(Event(s)); } catch(InvalidDataException) { rejected=true; }Check(rejected); });
Test("cpu_actual_elapsed_host_capacity","core",()=>Check(Measurements.Cpu(2,4,2,4)==25));
Test("network_actual_elapsed_and_reset","core",()=>Check(Measurements.Rate(100,200,2)==50 && Measurements.Rate(200,100,2)is null));
Test("summary_weights_real_intervals","core",()=> { var w=new SummaryWindow();var a=Sample(5,false,10);var b=Sample(20,false,90);w.Add(a,a.ObservedAt,5);w.Add(b,b.ObservedAt,15);Check(w.Build().Processes.Single().CpuPercentHostCapacity.Mean==70); });
foreach(var secret in new[]{"password=Secret123","--token abc123","https://user:password@example","Server=host;Password=test","Authorization: Bearer SuperSecret","--api-key secret","session_token=TopSecret"})
    Test("redaction_"+Array.IndexOf(new[]{"password=Secret123","--token abc123","https://user:password@example","Server=host;Password=test","Authorization: Bearer SuperSecret","--api-key secret","session_token=TopSecret"},secret),"privacy",()=>
    {
        var redacted=Privacy.SummarizeArguments(secret.Split(' ')).Arguments;
        var win=CryptoGuard.Compatibility.Windows.Core.Privacy.ArgumentFeatures(secret);
        Check(!string.Join(' ',redacted).Contains(secret,StringComparison.Ordinal) && !string.Join(' ',win).Contains(secret,StringComparison.Ordinal));
        var dir=Temp();using var spool=new DurableSpool(new() { StateDirectory=dir });var e=Event(Sample(0));e.AgentId=spool.State.AgentId;e.Sequence=spool.NextSequence();e.Snapshot!.Processes[0].RedactedArguments=redacted;
        Check(spool.Append(e));using var output=new StringWriter();LocalCommands.Export(dir,output);Check(!output.ToString().Contains(secret,StringComparison.Ordinal));
    });
Test("spool_uid_collision_rejected","core",()=> { using var spool=new DurableSpool(new() { StateDirectory=Temp() });var e=Event(Sample(0));e.AgentId=spool.State.AgentId;e.Sequence=spool.NextSequence();Check(spool.Append(e));e.SampleDurationMs=999;bool rejected=false;try { spool.Append(e); }catch(InvalidDataException){rejected=true;}Check(rejected); });
Test("pending_risk_retry_is_not_a_dropped_event","core",()=> { using var spool=new DurableSpool(new() { StateDirectory=Temp(),SpoolLimitBytes=100,RiskReserveBytes=0 });var e=Event(Sample(0));e.AgentId=spool.State.AgentId;e.Sequence=spool.NextSequence();e.EventType="risk";Check(!spool.Append(e) && !spool.Append(e) && spool.State.DroppedEvents==0); });
Test("local_sink_requires_ack","core",()=> { using var spool=new DurableSpool(new() { StateDirectory=Temp() });var e=Event(Sample(0));e.AgentId=spool.State.AgentId;e.Sequence=spool.NextSequence();Check(spool.Append(e));var sink=new LocalFakeReceiver();Check(Delivery.TryDeliverAsync(spool,sink,CancellationToken.None).Result==0 && spool.ReadAll().Count()==1);sink.Acknowledge=true;Check(Delivery.TryDeliverAsync(spool,sink,CancellationToken.None).Result==1 && !spool.ReadAll().Any()); });
Test("feature_contract_has_22_unique_named_units","ml",()=> { var s=Sample(0);var f=Features.Extract(s,s.Processes[0],0);Check(f.Count==22 && Features.Names.Distinct().Count()==22 && f.Keys.SequenceEqual(Features.Names) && f["network_tx_bytes_per_sec"].Unit=="bytes_per_second"); });
Test("ml_missing_features_are_null","ml",()=> { var s=Sample(0,false,null);var f=Features.Extract(s,s.Processes[0]);Check(f["process_cpu_percent_host_capacity"].Value is null && f["device_gpu_percent"].Value is null); });
string ModelFile(ForestModel model,out string hash) { var dir=Temp();Directory.CreateDirectory(dir);var path=Path.Combine(dir,"model.json");var bytes=JsonSerializer.SerializeToUtf8Bytes(model,MlJson.Default.ForestModel);File.WriteAllBytes(path,bytes);hash=Convert.ToHexString(SHA256.HashData(bytes));return path; }
ForestModel Model()=>new() { ModelVersion="test-only",FeatureNames=Features.Names,TrainingMedians=new double[22],Trees=[[new() { SuspiciousFraction=0.9 }]] };
Test("ml_model_hash_verification","ml",()=> { var path=ModelFile(Model(),out _);bool rejected=false;try { ShadowForest.Load(path,new string('0',64)); }catch(InvalidDataException){rejected=true;}Check(rejected); });
Test("ml_model_rejects_cycles","ml",()=> { var m=Model();m.Trees=[[new() { Feature=0,Threshold=50,Left=0,Right=0 }]];var path=ModelFile(m,out var hash);bool rejected=false;try { ShadowForest.Load(path,hash); }catch(InvalidDataException){rejected=true;}Check(rejected); });
Test("ml_shadow_cannot_raise_security_or_confirm","ml",()=> { var path=ModelFile(Model(),out var hash);var e=new RuleEngine(new() { MlShadowEnabled=true,MlModelPath=path,MlModelSha256=hash });var r=e.Evaluate(Sample(0,false,99)).Single();Check(r.Ml.Enabled && r.Ml.Prediction=="suspicious" && r.Ml.Mode=="shadow" && r.SecurityRisk==0 && r.Lifecycle=="observed" && r.AssessmentSource=="heuristic"); });
Test("ml_insufficient_coverage_abstains","ml",()=> { var path=ModelFile(Model(),out var hash);var r=ShadowForest.Load(path,hash).Predict([]);Check(r.Score is null && r.Prediction is null && r.Status=="insufficient_feature_coverage"); });
Test("ml_invalid_model_does_not_stop_heuristic","ml",()=> { var path=ModelFile(Model(),out _);var r=new RuleEngine(new() { MlShadowEnabled=true,MlModelPath=path,MlModelSha256=new string('0',64) }).Evaluate(Sample(0)).Single();Check(r.Ml.Status=="model_rejected" && r.Severity=="low"); });
Test("ml_extra_feature_does_not_change_prediction","ml",()=> { var path=ModelFile(Model(),out var hash);var model=ShadowForest.Load(path,hash);var s=Sample(0);var f=Features.Extract(s,s.Processes[0],0);var before=model.Predict(f);f["unrecognized"]=new(999,"ok","count");var after=model.Predict(f);Check(before.Score==after.Score && before.FeatureCoverage==after.FeatureCoverage); });
Test("ml_bad_json_rejected_without_execution","ml",()=> { var path=ModelFile(Model(),out _);File.WriteAllText(path,"{bad json");var hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));bool rejected=false;try { ShadowForest.Load(path,hash); }catch(JsonException){rejected=true;}Check(rejected); });
Test("ml_unsupported_format_version_rejected","ml",()=> { var m=Model();m.Format="sentinelzone-forest-json-999";var path=ModelFile(m,out var hash);bool rejected=false;try { ShadowForest.Load(path,hash); }catch(InvalidDataException){rejected=true;}Check(rejected); });
Test("ml_wrong_feature_contract_version_rejected","ml",()=> { var m=Model();m.FeatureContractVersion="future";var path=ModelFile(m,out var hash);bool rejected=false;try { ShadowForest.Load(path,hash); }catch(InvalidDataException){rejected=true;}Check(rejected); });
Test("ml_windows_linux_vector_prediction_equal","ml",()=> { var path=ModelFile(Model(),out var hash);var model=ShadowForest.Load(path,hash);var a=Sample(10);var b=Sample(10);b.Host.Platform="windows";var x=model.Predict(Features.Extract(a,a.Processes[0],10));var y=model.Predict(Features.Extract(b,b.Processes[0],10));Check(JsonSerializer.Serialize(x,ContractJson.Default.MlShadowResult)==JsonSerializer.Serialize(y,ContractJson.Default.MlShadowResult)); });
Test("linux_0221_identity_sequence_and_pending_survive","migration",()=> { var dir=Temp();Directory.CreateDirectory(dir);var id=Guid.NewGuid().ToString();File.WriteAllText(Path.Combine(dir,"identity.json"),"{\"agent_id\":\""+id+"\",\"sequence\":123,\"dropped_events\":0,\"recovered_partial_writes\":0}");var pending=Event(Sample(90,true),123);pending.AgentId=id;pending.SchemaVersion="1.0.0";pending.AgentVersion="0.22.1";pending.EventType="risk";var state=Confirmed().ExportState();var alertId=state.Single().AlertId;File.WriteAllBytes(Path.Combine(dir,"risk-checkpoint.json"),JsonSerializer.SerializeToUtf8Bytes(new RiskCheckpoint { States=state,PendingEvent=pending },ContractJson.Default.RiskCheckpoint));using var spool=new DurableSpool(new() { StateDirectory=dir });var checkpoint=new RiskCheckpointStore(dir);Check(spool.State.AgentId==id && spool.NextSequence()==124 && checkpoint.RestoreStates().Single().AlertId==alertId);Check(checkpoint.FlushPending(spool) && spool.ReadAll().Single().EventUid==pending.EventUid); });

var workloads=new[]{"idle","browser","video","compile","windows_update","antivirus_scan","backup","compression","gaming","video_encoding","approved_render","approved_ai_gpu","normal_cpu_stress","normal_ram_stress"};
foreach(var field in new[]{"trees","feature_names","training_medians"})
    Test("ml_null_"+field+"_fails_safe","ml",()=> { var path=ModelFile(Model(),out _);var node=JsonNode.Parse(File.ReadAllText(path))!;node[field]=null;File.WriteAllText(path,node.ToJsonString());var hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));var result=new RuleEngine(new() { MlShadowEnabled=true,MlModelPath=path,MlModelSha256=hash }).Evaluate(Sample(0)).Single();Check(result.Ml.Status=="model_rejected" && result.AssessmentSource=="heuristic"); });
Test("jwt_redacted_from_spool_export_and_features","privacy",()=> { const string jwt="eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJsYWIifQ.FIXTURE_SECRET";var s=Sample(0);s.Processes[0].RedactedArguments=Privacy.SummarizeArguments(new[]{"--session",jwt}).Arguments;var dir=Temp();using var spool=new DurableSpool(new() { StateDirectory=dir });var e=Event(s);e.AgentId=spool.State.AgentId;e.Sequence=spool.NextSequence();Check(spool.Append(e));using var output=new StringWriter();LocalCommands.Export(dir,output);Check(!output.ToString().Contains(jwt) && !JsonSerializer.Serialize(Features.Extract(s,s.Processes[0]),MlJson.Default.DictionaryStringFeatureValue).Contains(jwt)); });
var detection=new JsonArray();
foreach(var workload in workloads)
    Test("normal_workload_"+workload,"detection",()=> { var e=new RuleEngine(new());RiskResult? last=null;for(int t=0;t<=180;t+=5) { var s=Sample(t,false,workload=="idle"?1:workload is "browser" or "video"?25:99,workload);last=e.Evaluate(s).Single();Check(last.Severity=="low" && last.Lifecycle=="observed"); } detection.Add(new JsonObject { ["workload"]=workload,["test_type"]="canonical_fixture",["status"]="PASS",["security_risk"]=last!.SecurityRisk,["resource_impact"]=last.ResourceImpact }); });
Test("controlled_mining_like_confirmed_then_resolved","detection",()=> { var e=Confirmed();Check(e.ExportState().Single().Lifecycle=="confirmed");RiskResult? last=null;for(int t=95;t<=155;t+=5)last=e.Evaluate(Sample(t,false,0)).Single();Check(last!.Lifecycle=="resolved");detection.Add(new JsonObject { ["workload"]="controlled_suspicious",["test_type"]="offline_canonical_fixture_no_network",["status"]="PASS" }); });

string? Option(string flag) { var i=Array.IndexOf(args,flag);return i>=0 && i+1<args.Length?args[i+1]:null; }
if(Option("--fixtures") is string fixtureDirectory)
{
    Directory.CreateDirectory(fixtureDirectory);
    foreach(var name in new[]{"normal-browser","high-cpu-benign","mining-like","missing-gpu","persistence-plus-network"})
    {
        using var writer=new StreamWriter(Path.Combine(fixtureDirectory,name+".jsonl"));
        for(int t=0;t<=180;t+=5)
        {
            var s=Sample(t,name is "mining-like" or "persistence-plus-network" && t<100,name=="normal-browser"?20:t<100?99:0);
            if(name=="persistence-plus-network") { s.Persistence.Add(new("fixture","fixture-unit","/tmp/worker","process-a","ok"));s.Connections.Add(new("host","ipv4","127.0.0.1",55000,"192.0.2.1",3333,"established","0","process-a","ok")); }
            writer.WriteLine(JsonSerializer.Serialize(Event(s,t/5+1),ContractJson.Default.TelemetryEvent));
        }
    }
}
if(Option("--report") is string report)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);
    File.WriteAllText(report,new JsonObject { ["passed"]=passed,["failed"]=failed,["skipped"]=0,["runtime_os"]=System.Runtime.InteropServices.RuntimeInformation.OSDescription,["tests"]=results }.ToJsonString(new JsonSerializerOptions { WriteIndented=true }));
    File.WriteAllText(Path.Combine(Path.GetDirectoryName(report)!,"detection-evaluation.json"),new JsonObject { ["evaluation_scope"]="deterministic fixtures; live workload gates tracked separately",["cases"]=detection }.ToJsonString(new JsonSerializerOptions { WriteIndented=true }));
}
Console.WriteLine($"RESULT passed={passed} failed={failed} skipped=0");
return failed==0?0:1;
