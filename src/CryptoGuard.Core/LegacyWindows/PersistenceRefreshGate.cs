using CryptoGuard.Compatibility.Windows.Contracts;

namespace CryptoGuard.Compatibility.Windows.Core;

public sealed class PersistenceRefreshGate
{
    private readonly HashSet<string> seen=new();
    private bool pending;
    public void Observe(RiskResult[] risks)
    {
        var alive=risks.Select(r=>r.ProcessInstanceId).ToHashSet();seen.RemoveWhere(id=>!alive.Contains(id));
        foreach(var risk in risks)
            if(risk.Evidence.Any(e=>e.Group is "arguments" or "network" or "hash") && seen.Add(risk.ProcessInstanceId))pending=true;
    }
    public bool IsDue(double seconds,double lastScan,double periodicSeconds)=>seconds-lastScan>=periodicSeconds || pending && seconds-lastScan>=30;
    public void Refreshed()=>pending=false;
}
