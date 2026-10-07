using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CryptoGuard.Compatibility.Windows.Contracts;

namespace CryptoGuard.Agent.Windows;

internal static class ChildCollector
{
    // Redirected standard handles are inherited only by our own child; there is no network listener or public pipe endpoint.
    public static async Task<T> ReadAsync<T>(string executable, IEnumerable<string> args, JsonTypeInfo<T> type, int seconds, CancellationToken ct)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new IOException("Collector could not start.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(seconds));
        try
        {
            var output = ReadBoundedAsync(process.StandardOutput, 4 * 1024 * 1024, timeout.Token);
            var error = ReadBoundedAsync(process.StandardError, 8192, timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var text = await output; _ = await error;
            if (process.ExitCode != 0) throw new IOException("Collector returned an unavailable status.");
            return JsonSerializer.Deserialize(text, type) ?? throw new InvalidDataException("Collector returned an empty result.");
        }
        finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(CancellationToken.None); } }
    }
    private static async Task<string> ReadBoundedAsync(StreamReader reader, int limit, CancellationToken ct)
    {
        var text = new System.Text.StringBuilder(); var buffer = new char[4096]; int count;
        while ((count = await reader.ReadAsync(buffer, ct)) > 0) { if (text.Length + count > limit) throw new InvalidDataException("Collector output limit reached."); text.Append(buffer, 0, count); }
        return text.ToString();
    }
    internal static async Task<string> ReadFrameAsync(StreamReader reader, int limit, CancellationToken ct)
    {
        var text = new System.Text.StringBuilder();
        var buffer = new char[4096];
        while (true)
        {
            var count = await reader.ReadAsync(buffer, ct);
            if (count == 0) throw new IOException("Hardware host closed before a complete frame.");
            var end = Array.IndexOf(buffer, '\n', 0, count);
            var length = end < 0 ? count : end;
            if (text.Length + length > limit) throw new InvalidDataException("Hardware frame exceeds limit.");
            text.Append(buffer, 0, length);
            if (end < 0) continue;
            if (end + 1 != count) throw new InvalidDataException("Unsolicited hardware output.");
            return text.ToString().TrimEnd('\r');
        }
    }
    public static (string Exe, string[] Prefix) SelfCommand()
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("No process path.");
        return Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            ? (exe, [Path.Combine(AppContext.BaseDirectory,"CryptoGuardAgent.dll")]) : (exe, []);
    }
}
internal sealed class HardwareCollector(AgentConfig config) : IHardwareCollector,IAsyncDisposable
{
    private Process? process;
    private Task? stderr;
    public string Status { get; private set; } = "disabled";
    public async Task<Sensor[]> CollectAsync(CancellationToken ct)
    {
        if (!config.EnableHardware) return [];
        var host = Path.Combine(AppContext.BaseDirectory, "hardware", "CryptoGuardHardwareHost.exe");
        if (!File.Exists(host)) { Status = "component_not_installed"; return []; }
        try
        {
            if(process is null || process.HasExited)
            {
                await DisposeAsync();
                var info=new ProcessStartInfo(host){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
                info.ArgumentList.Add("--serve");if(config.EnableCpuHardware)info.ArgumentList.Add("--cpu");
                process=Process.Start(info)??throw new IOException("Hardware host could not start.");
                stderr=DrainAsync(process.StandardError);
            }
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(8));
            await process.StandardInput.WriteLineAsync("sample".AsMemory(),timeout.Token);await process.StandardInput.FlushAsync(timeout.Token);
            var line=await ChildCollector.ReadFrameAsync(process.StandardOutput,4*1024*1024,timeout.Token);
            var sensors=JsonSerializer.Deserialize(line,WireJson.Default.SensorArray)??throw new InvalidDataException("Empty hardware frame.");
            Status = sensors.Length == 0 ? "unsupported" : sensors.Any(s=>s.Value is null) ? "partial" : "ok"; return sensors;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.ComponentModel.Win32Exception or JsonException or OperationCanceledException)
        { await DisposeAsync();if (ct.IsCancellationRequested) throw; Status = "temporarily_unavailable"; return []; }
    }
    private static async Task DrainAsync(StreamReader reader)
    {try{var buffer=new char[1024];while(await reader.ReadAsync(buffer)>0){}}catch(IOException){} }
    public async ValueTask DisposeAsync()
    {
        if(process is null)return;
        try
        {
            if(!process.HasExited)
            {
                try{process.StandardInput.Close();}catch(IOException){}
                try{await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(1));}
                catch(TimeoutException)
                {
                    try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException) when(process.HasExited){}
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2));
                }
            }
            if(stderr is not null)await stderr;
        }
        finally{process.Dispose();process=null;stderr=null;}
    }
}
