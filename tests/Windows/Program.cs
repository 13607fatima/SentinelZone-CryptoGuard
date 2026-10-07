using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using CryptoGuard.Compatibility.Windows.Contracts;
using CryptoGuard.Compatibility.Windows.Core;
using CryptoGuard.Platform.Windows;
using CryptoGuard.Compatibility.Windows.Risk;

var passed=0; var failed=0;
var root=Path.GetFullPath(args.FirstOrDefault() ?? Path.Combine(Path.GetTempPath(),"cryptoguard-tests-"+Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(root);
void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
void Test(string name, Action body) { try { body(); passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex); } }
var now=DateTimeOffset.Parse("2026-10-04T10:00:00Z");
Metric Cpu(double? value)=>new(value,"percent_host_capacity",value is null?"warmup":"ok","fixture");
ProcessSample P(string id="p1",double? cpu=0,string[]? flags=null)=>new(id,42,1,now,@"C:\Apps\worker.exe",new string('a',64),"authenticode","unsigned",null,Cpu(cpu),1024,flags??[],"ok");
Snapshot S(ProcessSample? p=null,Connection[]? connections=null,PersistenceEntry[]? persistence=null)=>new(
    new("windows","fixture","x64","boot","test",8,Cpu(0),new(20,"percent","ok","fixture"),16000000000,
        Metric.Missing("bytes_per_second","fixture"),Metric.Missing("bytes_per_second","fixture")),[p??P()],connections??[],persistence??[],[],new(1,0,"ok","fixture"),[],[],[],new(0,0,0,0,[]));
EventEnvelope E(long sequence=1)=>new("1.0","telemetry",Guid.NewGuid().ToString("D"),Guid.NewGuid().ToString("D"),"0.22.0-alpha.3",sequence,now,5000,S());
string Dir(string name) { var d=Path.Combine(root,name+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(d);return d; }
Connection Net(string ip="192.0.2.1",int remotePort=443)=>new(42,"p1","ipv4","127.0.0.1",3333,ip,remotePort,"Established","fixture");
string[] mining=["stratum_protocol","pool_option","wallet_option"];
AgentConfig IocConfig()=>new(){Iocs=[new("ip","192.0.2.1","local-test-fixture","high",now.AddDays(1))]};

Test("CPU first sample is null",()=>Check(Measurements.Cpu(null,1,5,8) is null));
Test("CPU normalized to host capacity",()=>Check(Measurements.Cpu(0,5,5,8)==12.5));
Test("CPU handles rollback and invalid elapsed",()=>Check(Measurements.Cpu(10,5,5,8) is null && Measurements.Cpu(0,1,0,8) is null));
Test("Network rate uses actual elapsed",()=>Check(Measurements.Rate(100,400,1.5)==200));
Test("Network counter reset is unknown",()=>Check(Measurements.Rate(100,50,1) is null));
Test("PID reuse changes identity",()=>Check(StateStore.ProcessId("a","b",42,now)!=StateStore.ProcessId("a","b",42,now.AddSeconds(1))));
Test("Boot changes identity",()=>Check(StateStore.ProcessId("a","b",42,now)!=StateStore.ProcessId("a","c",42,now)));
Test("Identity and sequence survive restart",()=>{var d=Dir("identity");string id;using(var s=new StateStore(d)){id=s.AgentId;Check(s.ReserveSequence()==1);}using(var s=new StateStore(d)){Check(s.AgentId==id&&s.ReserveSequence()==2);}});
Test("Single writer enforced",()=>{var d=Dir("lock");using var s=new StateStore(d);bool rejected=false;try{using var other=new StateStore(d);}catch(IOException){rejected=true;}Check(rejected);});
Test("Command secrets never persisted",()=>{var raw="miner --url stratum+tcp://user:SECRET@host --user WALLET --password TOPSECRET --token=TOKEN 'Password=CONNECTION;'";var safe=string.Join(',',Privacy.ArgumentFeatures(raw));Check(safe.Contains("stratum_protocol")&&!safe.Contains("SECRET")&&!safe.Contains("TOKEN")&&!safe.Contains("WALLET")&&!safe.Contains("CONNECTION"));});
Test("Quoted Windows path parsed without arguments",()=>Check(Privacy.ExecutableFromCommand("\"C:\\Program Files\\Test\\a.exe\" --password secret")==@"C:\Program Files\Test\a.exe"));
Test("Ambiguous unquoted path never guessed",()=>Check(Privacy.ExecutableFromCommand(@"C:\Program Files\a.exe --x") is null));
Test("High CPU alone stays low",()=>{var r=new RulesEngine(new());RiskResult? last=null;for(int i=0;i<=12;i++)last=r.Evaluate(S(P(cpu:100)),now.AddSeconds(i*5),i*5)[0];Check(last!.Score==15&&last.Severity=="low"&&last.Assessment=="no_mining_specific_evidence");});
Test("Port-only connection gives no mining score",()=>Check(new RulesEngine(new()).Evaluate(S(connections:[Net(remotePort:3333)]),now,0)[0].Score==0));
Test("One generic argument is insufficient",()=>Check(new RulesEngine(new()).Evaluate(S(P(flags:["pool_option"])),now,0)[0].Score==0));
Test("Mining argument combination is one evidence group",()=>{var r=new RulesEngine(new()).Evaluate(S(P(flags:mining)),now,0)[0];Check(r.Score==25&&r.Evidence.Length==1&&r.Severity=="medium");});
Test("Two independent groups qualify high",()=>{var r=new RulesEngine(IocConfig()).Evaluate(S(P(flags:mining),[Net()]),now,0)[0];Check(r.Score==55&&r.Severity=="high");});
Test("Expired IOC excluded",()=>{var c=IocConfig() with{Iocs=[new("ip","192.0.2.1","test","high",now.AddSeconds(-1))]};Check(new RulesEngine(c).Evaluate(S(connections:[Net()]),now,0)[0].Score==0);});
Test("Wrong process network not attributed",()=>Check(new RulesEngine(IocConfig()).Evaluate(S(P(flags:mining),[Net() with{ProcessInstanceId="other"}]),now,0)[0].Score==25));
Test("Low network traffic cannot veto evidence",()=>Check(new RulesEngine(IocConfig()).Evaluate(S(P(flags:mining),[Net()]),now,0)[0].Score==55));
Test("Signature does not cancel mining evidence",()=>Check(new RulesEngine(IocConfig()).Evaluate(S(P(flags:mining) with{TrustStatus="trusted_offline"},[Net()]),now,0)[0].Score==55));
Test("Authorized mining retains activity evidence",()=>{var p=P(flags:mining);var c=new AgentConfig{Authorization=[new(p.Sha256!,p.ExePath!,null,"allowed")]};var r=new RulesEngine(c).Evaluate(S(p),now,0)[0];Check(r.Score==25&&r.Assessment=="authorized_mining_indicators");});
Test("Wrong hash cannot authorize",()=>{var p=P(flags:mining);var c=new AgentConfig{Authorization=[new(new string('b',64),p.ExePath!,null,"allowed")]};Check(new RulesEngine(c).Evaluate(S(p),now,0)[0].Authorization=="unknown");});
Test("Persistence alone gives no threat",()=>Check(new RulesEngine(new()).Evaluate(S(persistence:[new("Run","fixture",P().ExePath,"observed")]),now,0)[0].Score==0));
Test("Missing CPU cannot create load evidence",()=>Check(new RulesEngine(new()).Evaluate(S(P(cpu:null)),now,0)[0].Score==0));
Test("Missing CPU cannot reuse stale high load",()=>{var r=new RulesEngine(new());for(int i=0;i<=6;i++)r.Evaluate(S(P(cpu:100)),now.AddSeconds(i*5),i*5);Check(r.Evaluate(S(P(cpu:null)),now.AddSeconds(35),35)[0].Score==0);});
Test("Collection gap breaks sustained compute window",()=>{var r=new RulesEngine(new());r.Evaluate(S(P(cpu:100)),now,0);Check(r.Evaluate(S(P(cpu:100)),now.AddSeconds(60),60)[0].Score==0);});
Test("PID reuse does not inherit CPU window",()=>{var r=new RulesEngine(new());for(int i=0;i<=6;i++)r.Evaluate(S(P(cpu:100)),now.AddSeconds(i*5),i*5);Check(r.Evaluate(S(P("p2",100)),now.AddSeconds(35),35)[0].Score==0);});
Test("Deterministic replay",()=>{var a=new RulesEngine(IocConfig());var b=new RulesEngine(IocConfig());for(int i=0;i<20;i++){var snap=S(P(cpu:50,flags:mining),[Net()]);Check(JsonSerializer.Serialize(a.Evaluate(snap,now.AddSeconds(i*5),i*5),WireJson.Default.RiskResultArray)==JsonSerializer.Serialize(b.Evaluate(snap,now.AddSeconds(i*5),i*5),WireJson.Default.RiskResultArray));}});
Test("Lifecycle uses elapsed seconds",()=>{var life=new AlertLifecycle();var r=new RulesEngine(new()).Evaluate(S(P(flags:mining)),now,0);Check(life.Update(r,now,0).Single().State=="suspected");for(int i=1;i<12;i++)Check(life.Update(r,now.AddSeconds(i*5),i*5).Length==0);Check(life.Update(r,now.AddSeconds(60),60).Single().State=="confirmed");Check(life.Update(r,now.AddSeconds(65),65).Length==0);});
Test("Lifecycle gaps do not confirm",()=>{var life=new AlertLifecycle();var r=new RulesEngine(new()).Evaluate(S(P(flags:mining)),now,0);life.Update(r,now,0);Check(life.Update(r,now.AddSeconds(120),120).Length==0);});
Test("Lifecycle resolves continuously observed absence",()=>{var life=new AlertLifecycle();var r=new RulesEngine(new()).Evaluate(S(P(flags:mining)),now,0);life.Update(r,now,0);for(int i=5;i<=60;i+=5)Check(life.Update([],now.AddSeconds(i),i).Length==0);Check(life.Update([],now.AddSeconds(65),65).Single().State=="resolved");});
Test("Trusted malicious hash has independent critical rule",()=>{var c=new AgentConfig{Iocs=[new("sha256",P().Sha256!,"test","high",now.AddDays(1))]};Check(new RulesEngine(c).Evaluate(S(),now,0)[0].Severity=="critical");});
Test("Contract roundtrip preserves null and UTC",()=>{var e=E() with{Data=S(P(cpu:null))};var json=JsonSerializer.Serialize(e,WireJson.Default.EventEnvelope);var copy=JsonSerializer.Deserialize(json,WireJson.Default.EventEnvelope)!;Check(copy.EventUid==e.EventUid&&copy.Data.Processes[0].CpuPercentHostCapacity.Value is null&&copy.ObservedAt.Offset==TimeSpan.Zero&&json.Contains("process_instance_id"));});
Test("Contract rejects missing required fields",()=>{bool rejected=false;try{JsonSerializer.Deserialize("{}",WireJson.Default.EventEnvelope);}catch(JsonException){rejected=true;}Check(rejected);});
Test("Contract rejects unexpected raw command field",()=>{var json=JsonSerializer.Serialize(E(),WireJson.Default.EventEnvelope);json=json.Insert(1,"\"raw_command_line\":\"secret\",");bool rejected=false;try{JsonSerializer.Deserialize(json,WireJson.Default.EventEnvelope);}catch(JsonException){rejected=true;}Check(rejected);});
Test("Spool append restart export and explicit ACK",()=>{var d=Dir("spool");var e=E();var a=new FileSpool(d,new());Check(a.Append(e,false));var b=new FileSpool(d,new());Check(b.Read().Single().EventUid==e.EventUid);var export=Path.Combine(root,"export.jsonl");b.Export(export);Check(File.ReadAllText(export).Contains(e.EventUid)&&b.Read().Count()==1);Check(b.Acknowledge(e.EventUid)&&!b.Read().Any());});
Test("Partial write recovery reports loss",()=>{var d=Dir("partial");File.WriteAllText(Path.Combine(d,"pending.tmp"),"{half");var a=new FileSpool(d,new());Check(a.RecoveredIncompleteWrites==1&&a.DroppedEvents==1);});
Test("Corrupt record does not stop startup",()=>{var d=Dir("corrupt");File.WriteAllText(Path.Combine(d,"bad.json"),"{half");var a=new FileSpool(d,new());Check(!a.Read().Any()&&a.DroppedEvents==1);});
Test("Spool priority reserve and explicit loss",()=>{var d=Dir("budget");var c=new AgentConfig{SpoolMaxBytes=65536,RiskReserveBytes=60000};var a=new FileSpool(d,c);for(int i=1;i<20;i++)a.Append(E(i),false);Check(a.DroppedEvents>0&&a.Append(E(100),true)&&a.Bytes<=c.SpoolMaxBytes);});
Test("Retention loss survives restart",()=>{var d=Dir("retention");var a=new FileSpool(d,new());a.Append(E(),false);foreach(var f in Directory.GetFiles(d,"*.json"))File.SetLastWriteTimeUtc(f,DateTime.UtcNow.AddDays(-10));a=new FileSpool(d,new());a.Append(E(2),false);Check(new FileSpool(d,new()).DroppedEvents==1);});
Test("Duplicate spool write preserves event UID",()=>{var a=new FileSpool(Dir("duplicate"),new());var e=E();Check(a.Append(e,false)&&a.Append(e,false)&&a.Read().Count()==1);});
Test("ACK only after successful fake receiver",()=>{var a=new FileSpool(Dir("delivery"),new());var e=E();a.Append(e,false);var receiver=new TestSink(true);Check(Delivery.TrySendAsync(a,receiver,e,0,CancellationToken.None).GetAwaiter().GetResult());Check(receiver.Seen==e.EventUid&&!a.Read().Any());});
Test("Unacknowledged event retained on cancellation",()=>{var a=new FileSpool(Dir("retry"),new());var e=E();a.Append(e,false);var receiver=new TestSink(false);using var ct=new CancellationTokenSource();ct.Cancel();try{Delivery.TrySendAsync(a,receiver,e,0,ct.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){}Check(a.Read().Single().EventUid==receiver.Seen);});
Test("Offline Authenticode returns signer for runtime host",()=>{var path=Environment.ProcessPath!;var inspected=new ExecutableInspector().Inspect(path);Check(inspected.Hash?.Length==64&&inspected.Trust=="trusted_offline"&&inspected.Signer?.Contains("Microsoft")==true);});
Test("Catalog-only Windows file verifies with Microsoft signer",()=>{var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"cmd.exe");var inspected=new ExecutableInspector().Inspect(path);Check(inspected.Hash?.Length==64&&inspected.Trust=="trusted_offline"&&inspected.Signer?.Contains("Microsoft")==true);});
Test("CPU history survives short same-boot restart",()=>{var old=new RulesEngine(new());for(int i=0;i<=6;i++)old.Evaluate(S(P(cpu:100)),now.AddSeconds(i*5),i*5);var fresh=new RulesEngine(new());fresh.RestoreHistory(old.ExportHistory(),35);Check(fresh.Evaluate(S(P(cpu:100)),now.AddSeconds(35),35)[0].Score==15);});
Test("Long restart gap discards compute history",()=>{var old=new RulesEngine(new());for(int i=0;i<=6;i++)old.Evaluate(S(P(cpu:100)),now.AddSeconds(i*5),i*5);var fresh=new RulesEngine(new());fresh.RestoreHistory(old.ExportHistory(),120);Check(fresh.Evaluate(S(P(cpu:100)),now.AddSeconds(120),120)[0].Score==0);});
Test("Confirmed alert ID survives restart without duplicate",()=>{var old=new AlertLifecycle();var r=new RulesEngine(new()).Evaluate(S(P(flags:mining)),now,0);for(int i=0;i<=12;i++)old.Update(r,now.AddSeconds(i*5),i*5);var saved=old.ExportState();var fresh=new AlertLifecycle();fresh.RestoreState(saved,65);Check(fresh.Update(r,now.AddSeconds(65),65).Length==0&&fresh.ExportState().Single().Alert.AlertId==saved.Single().Alert.AlertId);});
Test("Offline time cannot confirm suspected alert",()=>{var old=new AlertLifecycle();var r=new RulesEngine(new()).Evaluate(S(P(flags:mining)),now,0);old.Update(r,now,0);var fresh=new AlertLifecycle();fresh.RestoreState(old.ExportState(),120);Check(fresh.Update(r,now.AddSeconds(120),120).Length==0&&fresh.ExportState().Single().Alert.State=="suspected");});
Test("Checkpoint requires same boot and rules version",()=>{var path=Path.Combine(Dir("checkpoint"),"state.json");DetectionState.Save(path,new("1.0","boot","v1",20,[],[]));Check(DetectionState.Load(path,"boot","v1",25) is not null&&DetectionState.Load(path,"newboot","v1",25) is null&&DetectionState.Load(path,"boot","v2",25) is null);});
Test("Truncated checkpoint is rejected without partial state",()=>{var path=Path.Combine(Dir("bad-checkpoint"),"state.json");File.WriteAllText(path,"{\"format_version\":\"1.0\"");bool rejected=false;try{DetectionState.Load(path,"boot","v1",25);}catch(JsonException){rejected=true;}Check(rejected);});
Test("Null checkpoint array entries are rejected as invalid data",()=>{var path=Path.Combine(Dir("null-checkpoint"),"state.json");File.WriteAllText(path,"{\"format_version\":\"1.0\",\"boot_id\":\"boot\",\"ruleset_version\":\"v1\",\"saved_monotonic_seconds\":20,\"histories\":[null],\"alerts\":[null]}");var saved=DetectionState.Load(path,"boot","v1",25)!;var rejected=0;try{new RulesEngine(new()).RestoreHistory(saved.Histories,25);}catch(InvalidDataException){rejected++;}try{new AlertLifecycle().RestoreState(saved.Alerts,25);}catch(InvalidDataException){rejected++;}Check(rejected==2);});
Test("Schema 1.0 event without summary remains readable",()=>{var json=JsonSerializer.Serialize(E(),WireJson.Default.EventEnvelope).Replace(",\"summary\":null","");var copy=JsonSerializer.Deserialize(json,WireJson.Default.EventEnvelope)!;Check(copy.SchemaVersion=="1.0"&&copy.Data.Summary is null&&copy.Data.Processes.Length==1);});
Test("Suspicion refresh uses cooldown and new process identity",()=>{var gate=new PersistenceRefreshGate();var r=new RulesEngine(new()).Evaluate(S(P(flags:mining)),now,0);gate.Observe(r);Check(!gate.IsDue(5,0,900)&&gate.IsDue(30,0,900));gate.Refreshed();gate.Observe(r);Check(!gate.IsDue(60,30,900));gate.Observe([r[0] with{ProcessInstanceId="p2"}]);Check(gate.IsDue(60,30,900));});
Test("Summary weights CPU by real elapsed duration",()=>{var window=new SummaryWindow();var a=S();window.Add(a with{Host=a.Host with{CpuPercentHostCapacity=Cpu(10)}},now,1);window.Add(a with{Host=a.Host with{CpuPercentHostCapacity=Cpu(30)}},now.AddSeconds(3),3);var summary=window.Build();Check(summary.HostCpuPercent.Mean==25&&summary.DurationMs==4000&&summary.SampleCount==2);});
Test("Summary excludes unknown values from mean",()=>{var window=new SummaryWindow();var a=S();window.Add(a with{Host=a.Host with{CpuPercentHostCapacity=Cpu(null)}},now,5);window.Add(a with{Host=a.Host with{CpuPercentHostCapacity=Cpu(40)}},now.AddSeconds(5),5);var metric=window.Build().HostCpuPercent;Check(metric.Mean==40&&metric.ValidSamples==1&&metric.TotalSamples==2&&metric.CoveredSeconds==5);});
Test("Summary keeps reused PIDs in different process instances",()=>{var window=new SummaryWindow();window.Add(S(P("p1",20)),now,5);window.Add(S(P("p2",40)),now.AddSeconds(5),5);Check(window.Build().Processes.Length==2);});
Test("Missing CPU interrupts sustained observation",()=>{var engine=new RulesEngine(new());for(int i=0;i<=25;i+=5)engine.Evaluate(S(P(cpu:100)),now.AddSeconds(i),i);engine.Evaluate(S(P(cpu:null)),now.AddSeconds(30),30);Check(engine.Evaluate(S(P(cpu:100)),now.AddSeconds(35),35)[0].Score==0);});
Test("Present process clear gap does not resolve",()=>{var life=new AlertLifecycle();var r=new RulesEngine(new()).Evaluate(S(P(flags:mining)),now,0);life.Update(r,now,0);var clean=new RulesEngine(new()).Evaluate(S(),now,0);life.Update(clean,now.AddSeconds(5),5);Check(life.Update(clean,now.AddSeconds(100),100).Length==0);});
Test("Absent process clear gap does not resolve",()=>{var life=new AlertLifecycle();var r=new RulesEngine(new()).Evaluate(S(P(flags:mining)),now,0);life.Update(r,now,0);life.Update([],now.AddSeconds(5),5);Check(life.Update([],now.AddSeconds(100),100).Length==0);});
Test("Spool UID collision rejected",()=>{var spool=new FileSpool(Dir("collision"),new());var item=E();spool.Append(item,false);bool rejected=false;try{spool.Append(item with{Sequence=2},true);}catch(InvalidDataException){rejected=true;}Check(rejected&&spool.Read().Single().Sequence==1);});
Test("Export works with live writer without changing state",()=>{var d=Dir("live-export");using var writer=new StateStore(d);var s=new FileSpool(Path.Combine(d,"spool"),new());var item=E();s.Append(item,false);var path=Path.Combine(root,"live-export.jsonl");FileSpool.ExportSnapshot(Path.Combine(d,"spool"),path);Check(File.ReadAllText(path).Contains(item.EventUid)&&s.Read().Count()==1&&s.DroppedEvents==0);});
Test("Export cannot overwrite spool",()=>{var d=Dir("bad-export");var s=new FileSpool(d,new());var item=E();s.Append(item,false);bool rejected=false;try{FileSpool.ExportSnapshot(d,Directory.GetFiles(d,"*.json").Single());}catch(InvalidDataException){rejected=true;}Check(rejected&&s.Read().Single().EventUid==item.EventUid);});
Test("Risk outbox survives full spool and restart without lost transition",()=>{
    var d=Dir("outbox-full");var path=Path.Combine(d,"checkpoint.json");var s=new FileSpool(Path.Combine(d,"spool"),new(){SpoolMaxBytes=65536,RiskReserveBytes=0});
    long sequence=1;while(s.Append(E(sequence++),false)){} // Fill to less than one event of headroom.
    var risk=new RulesEngine(new()).Evaluate(S(P(flags:mining)),now,0);var life=new AlertLifecycle();var changes=life.Update(risk,now,0);
    var item=E(sequence) with{EventType="risk",Data=S() with{Alerts=changes,Risks=risk}};
    var journal=new DetectionJournal(path);Check(!journal.Commit(new("1.1","boot","v1",0,[],life.ExportState()),item,s));
    var losses=s.DroppedEvents;Check(!journal.FlushPending(s)&&s.DroppedEvents==losses);
    journal=new DetectionJournal(path);Check(journal.Current!.PendingEvent!.EventUid==item.EventUid&&journal.Current.Alerts.Single().Alert.AlertId==changes.Single().AlertId);
    foreach(var saved in s.Read().ToArray())s.Acknowledge(saved.EventUid);
    Check(journal.FlushPending(s)&&journal.Current.PendingEvent is null&&s.Read().Single().EventUid==item.EventUid);
});
Test("Append before checkpoint clear recovers exactly one event across boot",()=>{
    var d=Dir("outbox-crash");var path=Path.Combine(d,"checkpoint.json");var s=new FileSpool(Path.Combine(d,"spool"),new());
    var risk=new RulesEngine(new()).Evaluate(S(P(flags:mining)),now,0);var changes=new AlertLifecycle().Update(risk,now,0);
    var item=E() with{EventType="risk",Data=S() with{Alerts=changes}};
    DetectionState.Save(path,new("1.1","oldboot","oldrules",0,[],[]){PendingEvent=item});s.Append(item,true);
    Check(DetectionState.Load(path,"newboot","newrules",0) is null);
    var journal=new DetectionJournal(path);Check(journal.FlushPending(s)&&s.Read().Count()==1&&s.Read().Single().EventUid==item.EventUid&&new DetectionJournal(path).Current!.PendingEvent is null);
});
Test("Corrupt risk journal is preserved rather than reset",()=>{var path=Path.Combine(Dir("corrupt-journal"),"state.json");File.WriteAllText(path,"{bad");bool rejected=false;try{new DetectionJournal(path);}catch(JsonException){rejected=true;}Check(rejected&&File.ReadAllText(path)=="{bad");});
Test("Journal write failure cannot acknowledge risk event",()=>{
    var d=Dir("blocked-journal");var path=Path.Combine(d,"state.json");var journal=new DetectionJournal(path);Directory.CreateDirectory(path);
    var spool=new FileSpool(Path.Combine(d,"spool"),new());bool rejected=false;
    try{journal.Commit(new("1.1","boot","v1",0,[],[]),null,spool);}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){rejected=true;}
    Check(rejected&&journal.Current is null&&!spool.Read().Any());
});
Test("Spool accounting updates after ACK and restart",()=>{var d=Dir("index");var s=new FileSpool(d,new());for(int i=1;i<=30;i++)s.Append(E(i),false);var expected=Directory.GetFiles(d,"*.json").Sum(f=>new FileInfo(f).Length);Check(s.Bytes==expected);foreach(var item in s.Read().Take(10).ToArray())s.Acknowledge(item.EventUid);Check(s.Bytes==new FileSpool(d,new()).Bytes&&s.Read().Count()==20);});
Test("Host CPU warmup and memory API",()=>{var h=new HostCollector();var s=h.Collect();Check(s.CpuPercentHostCapacity.Value is null&&s.TotalMemoryBytes>0&&s.LogicalCpuCount>0);Thread.Sleep(100);Check(h.Collect().CpuPercentHostCapacity.Value is >=0 and <=100);});
Test("TCP IPv4 maps established loopback to PID",()=>Loopback(AddressFamily.InterNetwork));
Test("TCP IPv6 maps established loopback to PID",()=>Loopback(AddressFamily.InterNetworkV6));
if(args.Contains("--interactive"))
{
    Test("Idle pipe accepts bound interactive session",()=>PipeRoundtrip(false));
    Test("Idle pipe rejects spoofed session identifier",()=>PipeRoundtrip(true));
}
void PipeRoundtrip(bool spoof)
{
    var bridge=new IdleBridge();using var ct=new CancellationTokenSource();
    var serving=bridge.ServeAsync(ct.Token);
    try
    {
        var sample=new IdleCollector().Collect();Check(sample.SessionId>0);
        if(spoof)sample=sample with{SessionId=sample.SessionId+1000};
        IdleBridge.SendAsync(sample,ct.Token).GetAwaiter().GetResult();
        if(spoof){Thread.Sleep(250);Check(bridge.Collect().Status!="ok");}
        else{Check(SpinWait.SpinUntil(()=>bridge.Collect().Status=="ok",3000));}
    }
    finally{ct.Cancel();try{serving.GetAwaiter().GetResult();}catch(OperationCanceledException){}}
}
void Loopback(AddressFamily family)
{
    var ip=family==AddressFamily.InterNetwork?IPAddress.Loopback:IPAddress.IPv6Loopback;
    var listener=new TcpListener(ip,0);listener.Start();
    try{using var client=new TcpClient(family);client.Connect((IPEndPoint)listener.LocalEndpoint);using var peer=listener.AcceptTcpClient();var id=Process.GetCurrentProcess().Id;var rows=new NetworkCollector().Collect(new Dictionary<int,string>{{id,"this"}});Check(rows.Any(c=>c.Pid==id&&c.ProcessInstanceId=="this"&&c.RemotePort==((IPEndPoint)listener.LocalEndpoint).Port&&c.State=="Established"));}
    finally{listener.Stop();}
}
Console.WriteLine($"RESULT passed={passed} failed={failed}");return failed==0?0:1;

sealed class TestSink(bool acknowledged):IEventSink
{
    public string? Seen;
    public Task<bool> SendAsync(EventEnvelope envelope,CancellationToken cancellationToken){Seen=envelope.EventUid;return Task.FromResult(acknowledged);}
}
