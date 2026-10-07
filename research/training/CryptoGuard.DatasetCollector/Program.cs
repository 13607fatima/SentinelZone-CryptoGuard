using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CryptoGuard.Contracts;
using CryptoGuard.Core;
using CryptoGuard.ML;

string Required(string name) { int i=Array.IndexOf(args,name);return i>=0 && i+1<args.Length?args[i+1]:throw new ArgumentException(name); }
var input=Required("--input");var output=Required("--output");var session=Required("--session-id");var host=Required("--host-id");var workload=Required("--workload");
var labels=new[]{"idle","browser","video","compile","backup","antivirus_scan","approved_render","approved_ai_gpu","normal_cpu_stress","normal_ram_stress","controlled_suspicious","controlled_mining_lab","windows_update","compression","gaming","video_encoding"};
var targetIndex=Array.IndexOf(args,"--process-instance-id");
string? target=targetIndex>=0 && targetIndex+1<args.Length?args[targetIndex+1]:null;
if(workload is "controlled_suspicious" or "controlled_mining_lab" && target is null && !args.Contains("--fixture"))throw new InvalidDataException("positive_label_requires_exact_process_instance");
if(!labels.Contains(workload) || !Guid.TryParse(session,out _) || new FileInfo(input).Length>256L*1024*1024)throw new InvalidDataException("dataset_arguments");
// Hash the supplied pseudonym again; no machine name, IP, executable path or argument value enters the dataset.
var hostId=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(host))).ToLowerInvariant();
using var writer=new StreamWriter(new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.Read));
DateTimeOffset? started=null;
var sustained=new Dictionary<string,double>();
double lastMs=0;
foreach(var line in File.ReadLines(input))
{
    if(line.Length>16*1024*1024)throw new InvalidDataException("dataset_event_limit");
    var e=JsonSerializer.Deserialize(line,ContractJson.Default.TelemetryEvent) ?? throw new InvalidDataException("dataset_event");LocalCommands.Validate(e);
    if(e.Snapshot is not Snapshot s)continue;
    started??=e.ObservedAt;
    if(s.MonotonicMs<lastMs || s.MonotonicMs-lastMs>15000)sustained.Clear();
    lastMs=s.MonotonicMs;
    foreach(var p in s.Processes)
    {
        if(target is not null && p.ProcessInstanceId!=target)continue;
        bool load=p.CpuPercentHostCapacity is { Status:"ok",Value:>=15 } || p.GpuPercent is { Status:"ok",Value:>=70 };
        if(load)sustained.TryAdd(p.ProcessInstanceId,s.MonotonicMs);else sustained.Remove(p.ProcessInstanceId);
        var features=Features.Extract(s,p,load?(s.MonotonicMs-sustained[p.ProcessInstanceId])/1000:null);
        var record=new JsonObject { ["session_id"]=session,["host_id"]=hostId,["platform"]=s.Host.Platform,["workload_label"]=workload,
            ["started_at"]=started.Value.ToString("O"),["observed_at"]=e.ObservedAt.ToString("O"),["feature_contract_version"]=ReleaseVersions.Features,
            ["record_source"]=args.Contains("--fixture")?"fixture":"agent_export",
            ["hardware_profile"]=new JsonObject { ["architecture"]=s.Host.Architecture,["logical_cpu_count"]=s.Host.LogicalCpuCount,["device_count"]=s.Devices.Count },
            ["features"]=JsonNode.Parse(JsonSerializer.Serialize(features,MlJson.Default.DictionaryStringFeatureValue)),
            ["label"]=workload is "controlled_suspicious" or "controlled_mining_lab"?1:0 };
        writer.WriteLine(record.ToJsonString());
    }
    var active=s.Processes.Select(p=>p.ProcessInstanceId).ToHashSet();foreach(var key in sustained.Keys.Where(k=>!active.Contains(k)).ToArray())sustained.Remove(key);
}
