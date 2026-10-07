using System.Text.Json;
using CryptoGuard.Compatibility.Windows.Contracts;

namespace CryptoGuard.Compatibility.Windows.Core;

public static class DetectionState
{
    private const int MaxBytes=16*1024*1024;
    public static DetectionCheckpoint? Load(string path,string bootId,string rulesetVersion,double now)
    {
        var state=Read(path);
        if(state is null || state.BootId!=bootId || state.RulesetVersion!=rulesetVersion || state.SavedMonotonicSeconds>now)return null;
        return state;
    }
    public static DetectionCheckpoint? Read(string path)
    {
        if(!File.Exists(path))return null;
        if(new FileInfo(path).Length>MaxBytes)throw new InvalidDataException("Detection checkpoint exceeds limit.");
        var state=JsonSerializer.Deserialize(File.ReadAllBytes(path),WireJson.Default.DetectionCheckpoint)
            ?? throw new InvalidDataException("Empty checkpoint.");
        if(state.FormatVersion is not ("1.0" or "1.1"))throw new InvalidDataException("Unsupported checkpoint.");
        if(!double.IsFinite(state.SavedMonotonicSeconds) || state.SavedMonotonicSeconds<0)throw new InvalidDataException("Invalid checkpoint time.");
        if(state.PendingEvent is { } pending && (pending.Data.Alerts.Length==0 || !Guid.TryParse(pending.EventUid,out _) || pending.Sequence<1))
            throw new InvalidDataException("Invalid pending risk event.");
        return state;
    }
    public static void Save(string path,DetectionCheckpoint state)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(state,WireJson.Default.DetectionCheckpoint);
        if(bytes.Length>MaxBytes)throw new InvalidDataException("Detection checkpoint exceeds limit.");
        AtomicFile.Write(path,bytes);
    }
}

// One pending event applies bounded backpressure. Never advance detection while
// an earlier transition cannot reach the spool. Recovery keeps the original UUID,
// including across boots/rules upgrades where CPU history is no longer reusable.
public sealed class DetectionJournal
{
    private readonly string path;
    public DetectionCheckpoint? Current { get; private set; }
    public DetectionJournal(string path) { this.path=path; Current=DetectionState.Read(path); }
    public bool FlushPending(ISpoolStore spool)
    {
        if(Current?.PendingEvent is not { } pending)return true;
        if(!spool.Append(pending,true))return false;
        Save(Current with { PendingEvent=null });
        return true;
    }
    public bool Commit(DetectionCheckpoint state,EventEnvelope? transition,ISpoolStore spool)
    {
        if(Current?.PendingEvent is not null)throw new InvalidOperationException("Pending transition must be flushed first.");
        if(transition is not null && transition.Data.Alerts.Length==0)throw new InvalidDataException("Missing alert transition.");
        Save(state with { FormatVersion="1.1",PendingEvent=transition });
        return FlushPending(spool);
    }
    private void Save(DetectionCheckpoint state)
    {
        // Any failure is fatal to the run; restart reads the last committed state.
        DetectionState.Save(path,state);
        Current=state;
    }
}
