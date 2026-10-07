using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using CryptoGuard.Compatibility.Windows.Contracts;
using Microsoft.Win32.SafeHandles;

namespace CryptoGuard.Platform.Windows;

// Advisory session telemetry only. This pipe accepts no commands, paths, executable names or policy changes.
public sealed class IdleBridge : IIdleCollector
{
    private const string PipeName="SentinelZone.CryptoGuard.Idle.v1";
    private readonly ConcurrentDictionary<int,(IdleSample Sample,long Seen)> sessions=new();
    [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe,out uint pid);
    public IdleSample Collect()
    {
        foreach(var (id,item) in sessions) if(Stopwatch.GetElapsedTime(item.Seen).TotalSeconds>30) sessions.TryRemove(id,out _);
        var values=sessions.Values.Select(s=>s.Sample).Where(s=>s.Status=="ok" && s.IdleSeconds is not null).ToArray();
        return values.Length==0 ? new(null,null,"temporarily_unavailable","interactive_helpers_no_fresh_report") :
            new(null,values.Min(s=>s.IdleSeconds),"ok","minimum_idle_across_reporting_sessions");
    }
    public async Task ServeAsync(CancellationToken ct)
    {
        var security=new PipeSecurity();
        security.SetAccessRuleProtection(true,false);
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid,null),PipeAccessRights.FullControl,AccessControlType.Deny));
        foreach(var sid in new[]{WellKnownSidType.LocalSystemSid,WellKnownSidType.LocalServiceSid,WellKnownSidType.BuiltinAdministratorsSid})
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(sid,null),PipeAccessRights.FullControl,AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.InteractiveSid,null),PipeAccessRights.ReadWrite,AccessControlType.Allow));
        while(!ct.IsCancellationRequested)
        {
            using var pipe=NamedPipeServerStreamAcl.Create(PipeName,PipeDirection.In,1,PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous|PipeOptions.FirstPipeInstance,1024,1024,security);
            await pipe.WaitForConnectionAsync(ct);
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(1000);
            try
            {
                if(!GetNamedPipeClientProcessId(pipe.SafePipeHandle,out var pid)) continue;
                using var client=Process.GetProcessById(checked((int)pid)); var sessionId=client.SessionId;
                if(sessionId==0) continue;
                using var reader=new StreamReader(pipe);var chars=new char[512];var total=0;
                while(total<chars.Length) {var n=await reader.ReadAsync(chars.AsMemory(total),timeout.Token);if(n==0)break;total+=n;}
                if(total==chars.Length)continue;
                var sample=JsonSerializer.Deserialize(new string(chars,0,total),WireJson.Default.IdleSample);
                if(sample?.SessionId!=sessionId || sample.Status!="ok" || sample.IdleSeconds is not double idle || !double.IsFinite(idle) || idle<0 || idle>uint.MaxValue/1000d)continue;
                if(sessions.Count<128 || sessions.ContainsKey(sessionId))sessions[sessionId]=(sample,Stopwatch.GetTimestamp());
            }
            catch(Exception ex) when(!ct.IsCancellationRequested && ex is IOException or JsonException or ArgumentException or InvalidOperationException or OperationCanceledException or System.ComponentModel.Win32Exception) { }
        }
    }
    public static async Task SendAsync(IdleSample sample,CancellationToken ct)
    {
        using var pipe=new NamedPipeClientStream(".",PipeName,PipeDirection.Out,PipeOptions.Asynchronous,TokenImpersonationLevel.Identification);
        await pipe.ConnectAsync(1000,ct);
        var bytes=JsonSerializer.SerializeToUtf8Bytes(sample,WireJson.Default.IdleSample);
        await pipe.WriteAsync(bytes,ct);await pipe.FlushAsync(ct);
    }
}
