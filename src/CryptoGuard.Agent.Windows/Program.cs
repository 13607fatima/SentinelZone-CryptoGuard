using System.Globalization;
using System.Text.Json;
using System.Text.Json.Schema;
using CryptoGuard.Agent.Windows;
using CryptoGuard.Compatibility.Windows.Contracts;
using CryptoGuard.Compatibility.Windows.Core;
using CryptoGuard.Platform.Windows;
using Canonical = CryptoGuard.Contracts;
using Shared = CryptoGuard.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

try
{
    var command = args.FirstOrDefault() ?? "help";
    if(command=="schema")
    {
        var schema=Canonical.ContractJson.Default.TelemetryEvent.GetJsonSchemaAsNode();
        schema["$schema"]="https://json-schema.org/draft/2020-12/schema";schema["title"]="CryptoGuard TelemetryEvent 1.2.0";
        Console.WriteLine(schema.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));return 0;
    }
    if (command is "version" or "--version") { Console.WriteLine(Canonical.ReleaseVersions.Agent); return 0; }
    if (command == "internal-metadata") { Console.WriteLine(JsonSerializer.Serialize(ProcessCollector.ReadMetadata(),WireJson.Default.ProcessMetadataArray)); return 0; }
    if (command == "internal-persistence") { Console.WriteLine(JsonSerializer.Serialize(new PersistenceCollector().Collect(),WireJson.Default.PersistenceEntryArray)); return 0; }
    if (command is "help" or "--help" or "-h")
    {
        Console.WriteLine("SentinelZone CryptoGuard 0.22.2-rc.1\nCommands:\n doctor [--data DIR] [--hardware]\n run [--data DIR] [--duration SECONDS] [--capture] [--hardware]\n export --data DIR --out FILE.jsonl\n replay --input FILE.jsonl [--config FILE]\n idle-helper (current session diagnostic)\n service --data DIR\nLocal detection only. Hardware component is optional. See README for release gates."); return 0;
    }
    string? Option(string name) { var i=Array.IndexOf(args,name); return i>=0 && i+1<args.Length ? args[i+1] : null; }
    if (command == "idle-helper")
    {
        using var idleStop=new CancellationTokenSource();Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;idleStop.Cancel();};
        do
        {
            var sample=new IdleCollector().Collect();
            if (!args.Contains("--watch")) { Console.WriteLine(JsonSerializer.Serialize(sample,WireJson.Default.IdleSample));return 0; }
            try { await IdleBridge.SendAsync(sample,idleStop.Token); } catch(Exception ex) when(ex is IOException or TimeoutException) { }
            await Task.Delay(10000,idleStop.Token);
        } while(true);
    }
    var data = Path.GetFullPath(Option("--data") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"SentinelZone","CryptoGuard"));
    var configFile = Option("--config") ?? Path.Combine(data,"config.json");
    if(Option("--config") is null && !File.Exists(configFile) && File.Exists(Path.Combine(data,"agent.json")))
    {
        // Retain the old config; copy once into the documented mutable location.
        File.Copy(Path.Combine(data,"agent.json"),configFile,false);
    }
    if(command=="status") { Shared.LocalCommands.Status(data,Console.Out); return 0; }
    var config = File.Exists(configFile) ? ConfigurationLoader.Load(configFile) : new AgentConfig();
    if (args.Contains("--hardware")) config = config with { EnableHardware=true };
    config.Validate();
    if (command == "export")
    {
        var output=Path.GetFullPath(Option("--out") ?? throw new ArgumentException("--out required."));
        if(output.StartsWith(data.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Export outside the agent state directory.");
        using(var writer=new StreamWriter(new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.Read))) Shared.LocalCommands.Export(data,writer);return 0;
    }
    if (command == "replay")
    {
        var policy=Normalization.Policy(config,data);
        Shared.Configuration.Validate(policy,validatePaths:false);
        CryptoGuard.Risk.ReplayRunner.Run(Option("--input") ?? throw new ArgumentException("--input required."),policy,Console.Out);
        return 0;
    }
    Directory.CreateDirectory(data);
    if (!File.Exists(configFile)) AtomicFile.Write(configFile,JsonSerializer.SerializeToUtf8Bytes(config,WireJson.Default.AgentConfig));
    if (command == "service")
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args=[],ContentRootPath=AppContext.BaseDirectory });
        builder.Logging.ClearProviders(); builder.Services.AddWindowsService(options=>options.ServiceName="SentinelZoneCryptoGuard");
        var bridge=new IdleBridge();
        builder.Services.AddSingleton(bridge);builder.Services.AddSingleton(new Runner(data,config,bridge)); builder.Services.AddHostedService<AgentService>();
        await builder.Build().RunAsync(); return Environment.ExitCode;
    }
    if (command is not ("run" or "doctor")) throw new ArgumentException("Unknown command. Use --help.");
    using var stop = new CancellationTokenSource(); Console.CancelKeyPress += (_,e)=>{e.Cancel=true;stop.Cancel();};
    var duration = double.Parse(Option("--duration") ?? "0",CultureInfo.InvariantCulture);
    if (!double.IsFinite(duration) || duration < 0) throw new ArgumentException("Invalid duration.");
    var result = await new Runner(data,config).RunAsync(duration,args.Contains("--capture"),command=="doctor",stop.Token);
    Console.WriteLine(JsonSerializer.Serialize(result,Canonical.ContractJson.Default.TelemetryEvent)); return 0;
}
catch (OperationCanceledException) { return 0; }
catch (Exception ex) when (ex is not OutOfMemoryException)
{
    // Never write exception messages: native providers may include raw command lines or sensitive paths.
    Console.Error.WriteLine("CryptoGuard failed: " + ex.GetType().Name); return 2;
}

internal sealed class AgentService(Runner runner, IdleBridge bridge) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        try
        {
            var collecting=runner.RunAsync(0,false,false,linked.Token);var serving=bridge.ServeAsync(linked.Token);
            var finished=await Task.WhenAny(collecting,serving);linked.Cancel();await finished;
            await Task.WhenAll(collecting,serving);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception) { Environment.Exit(1); } // Non-clean failure lets SCM recovery actions run.
    }
}
