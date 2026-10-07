using CryptoGuard.Contracts;

namespace CryptoGuard.Core;

// CPU/rates use measured interval lengths. Missing values never become zero or enter the mean.
public sealed class SummaryWindow
{
    private sealed class Accumulator(string unit)
    {
        private double weighted,totalSeconds,min=double.PositiveInfinity,max=double.NegativeInfinity;
        private int valid,total;
        public void Add(double? value,double duration)
        {
            total++;
            if(value is not double number || !double.IsFinite(number) || duration<=0)return;
            valid++;weighted+=number*duration;totalSeconds+=duration;min=Math.Min(min,number);max=Math.Max(max,number);
        }
        public SummaryMetric Build()=>new(valid==0?null:min,valid==0?null:max,totalSeconds>0?weighted/totalSeconds:null,unit,valid,total,totalSeconds);
    }
    private readonly Accumulator cpu=new("percent_host_capacity"),memory=new("percent"),rx=new("bytes_per_second"),tx=new("bytes_per_second");
    private readonly Dictionary<string,(Accumulator Cpu,Accumulator Rss)> processes=new();
    private DateTimeOffset? first,last;
    private double duration;
    private int count;
    public void Add(Snapshot snapshot,DateTimeOffset now,double elapsedSeconds)
    {
        if(!double.IsFinite(elapsedSeconds)||elapsedSeconds<0)throw new InvalidDataException("Invalid summary interval.");
        first??=now-TimeSpan.FromSeconds(elapsedSeconds);last=now;duration+=elapsedSeconds;count++;
        cpu.Add(snapshot.Host.CpuPercent.Value,elapsedSeconds);memory.Add(snapshot.Host.MemoryUsedPercent.Value,elapsedSeconds);
        rx.Add(snapshot.Host.NetworkReceiveBytesPerSecond.Value,elapsedSeconds);tx.Add(snapshot.Host.NetworkTransmitBytesPerSecond.Value,elapsedSeconds);
        foreach(var p in snapshot.Processes)
        {
            if(!processes.TryGetValue(p.ProcessInstanceId,out var aggregate))
            {
                if(processes.Count>=10000)continue;
                processes[p.ProcessInstanceId]=aggregate=(new("percent_host_capacity"),new("bytes"));
            }
            aggregate.Cpu.Add(p.CpuPercentHostCapacity.Value,elapsedSeconds);aggregate.Rss.Add(p.RssBytes.Value,elapsedSeconds);
        }
    }
    public TelemetrySummary Build()=>new(first??DateTimeOffset.UnixEpoch,last??DateTimeOffset.UnixEpoch,duration*1000,count,
        cpu.Build(),memory.Build(),rx.Build(),tx.Build(),processes.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>new ProcessSummary(p.Key,p.Value.Cpu.Build(),p.Value.Rss.Build())).ToArray());
}
