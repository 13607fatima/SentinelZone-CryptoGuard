using CryptoGuard.Contracts;
using CryptoGuard.Core;
using CryptoGuard.Platform.Linux;
using CryptoGuard.Risk;
using CryptoGuard.Agent.Linux;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Schema;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (!OperatingSystem.IsLinux()) { Console.Error.WriteLine("linux_required"); return 2; }
            LinuxDurability.Initialize();
            string command = args.FirstOrDefault() ?? "help";
            string? Option(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
            if (command is "help" or "--help") { Console.WriteLine("CryptoGuard 0.22.2-rc.1 | doctor | run --mode standalone|service | export | replay FILE | validate FILE\nOptions: --config FILE --state DIRECTORY --samples COUNT --output FILE\nLocal detection only. No active response or backend transport."); return 0; }
            if (command is "--version" or "version") { Console.WriteLine("0.22.2-rc.1"); return 0; }
            string? configPath = Option("--config");
            var config = Configuration.Load(configPath ?? (File.Exists("/etc/cryptoguard/agent.json") ? "/etc/cryptoguard/agent.json" : null));
            if (Option("--state") is string state) config.StateDirectory = Path.GetFullPath(state);
            if (Option("--socket") is string socketPath) config.CollectorSocket = Path.GetFullPath(socketPath);
            Configuration.Validate(config);
            using var stopped = new CancellationTokenSource();
            using var term = PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx => { ctx.Cancel = true; stopped.Cancel(); });
            using var interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, ctx => { ctx.Cancel = true; stopped.Cancel(); });
            switch (command)
            {
                case "schema": Console.WriteLine(ContractJson.Default.TelemetryEvent.GetJsonSchemaAsNode().ToJsonString()); return 0;
                case "doctor":
                    await using (var collector = new StandaloneCollector(config))
                    {
                        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        var snapshot = await collector.Collect(Guid.Empty.ToString(), deadline.Token);
                        // Doctor reports host/capability data, not process argument inventories.
                        CapabilitySummary.Enrich(snapshot,config); snapshot.Processes.Clear(); snapshot.Connections.Clear(); snapshot.Persistence.Clear();
                        Console.WriteLine(JsonSerializer.Serialize(snapshot, ContractJson.Default.Snapshot));
                    }
                    return 0;
                case "collect-stdio": await Worker(stopped.Token); return 0;
                case "collector": await Server(config, stopped.Token); return 0;
                case "run":
                    if (Option("--mode") is string mode && mode is not ("standalone" or "service")) throw new InvalidDataException("invalid_mode");
                    await Run(config, Option("--mode") == "service", int.TryParse(Option("--samples"), out int count) ? count : 0, stopped.Token); return 0;
                case "status": LocalCommands.Status(config.StateDirectory,Console.Out); return 0;
                case "export":
                    using (var output = Output(Option("--output")))
                        LocalCommands.Export(config.StateDirectory, output);
                    return 0;
                case "replay":
                    if (args.Length < 2) throw new InvalidDataException("input_required");
                    using (var output = Output(Option("--output")))
                        ReplayRunner.Run(Option("--input") ?? args[1],config,output);
                    return 0;
                case "validate":
                    if (args.Length < 2) throw new InvalidDataException("input_required");
                    int valid = 0;
                    foreach (string line in File.ReadLines(args[1])) { Validate(JsonSerializer.Deserialize(line, ContractJson.Default.TelemetryEvent) ?? throw new InvalidDataException("invalid_event")); valid++; }
                    Console.WriteLine($"validated_events={valid}"); return 0;
                default: Console.Error.WriteLine("unknown_command"); return 2;
            }
        }
        catch (OperationCanceledException) { return 0; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or SocketException or ArgumentException or InvalidOperationException)
        { Console.Error.WriteLine("cryptoguard_error:" + ex.GetType().Name); return 1; }
    }
    private static TextWriter Output(string? path) => path is null ? Console.Out : new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
    private static void Validate(TelemetryEvent e) => LocalCommands.Validate(e);
    private static void ExportReadOnly(string state, TextWriter output)
    {
        // Immutable files can be read while the service writes new files; a concurrent acknowledgement/expiry may remove one.
        foreach (string file in Directory.EnumerateFiles(Path.Combine(state, "spool"), "*.event").Order(StringComparer.Ordinal))
        {
            byte[] bytes;
            try { bytes = File.ReadAllBytes(file); } catch (FileNotFoundException) { continue; }
            string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
            if (!Path.GetFileName(file).Contains("-" + hash + "-", StringComparison.Ordinal)) throw new InvalidDataException("spool_checksum");
            var e = JsonSerializer.Deserialize(bytes, ContractJson.Default.TelemetryEvent) ?? throw new InvalidDataException("event_json");
            Validate(e);
            output.WriteLine(Encoding.UTF8.GetString(bytes));
        }
    }
    private static async Task Worker(CancellationToken token)
    {
        var input = Console.OpenStandardInput(); var output = Console.OpenStandardOutput();
        var config = JsonSerializer.Deserialize(await Frame.Read(input, 1048576, token), ContractJson.Default.AgentConfig) ?? throw new InvalidDataException("worker_config");
        Configuration.Validate(config);
        var collector = new LinuxCollector(config);
        while (!token.IsCancellationRequested)
        {
            byte[] request;
            try { request = await Frame.Read(input, 64, token); } catch (EndOfStreamException) { break; }
            string id = Encoding.UTF8.GetString(request);
            if (!Guid.TryParse(id, out _)) throw new InvalidDataException("agent_id");
            await Frame.Write(output, JsonSerializer.SerializeToUtf8Bytes(collector.Collect(id), ContractJson.Default.Snapshot), token);
        }
    }
    private static async Task Server(AgentConfig config, CancellationToken token)
    {
        string parent = Path.GetDirectoryName(config.CollectorSocket)!;
        Directory.CreateDirectory(parent);
        if (File.Exists(config.CollectorSocket)) File.Delete(config.CollectorSocket);
        using var server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        server.Bind(new UnixDomainSocketEndPoint(config.CollectorSocket));
        File.SetUnixFileMode(config.CollectorSocket, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite);
        server.Listen(4);
        var collector = new LinuxCollector(config);
        SystemdNotify.Send("READY=1");
        while (!token.IsCancellationRequested)
        {
            SystemdNotify.Send("WATCHDOG=1");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                using var client = await server.AcceptAsync(deadline.Token);
                using var stream = new NetworkStream(client);
                string id = Encoding.UTF8.GetString(await Frame.Read(stream, 64, deadline.Token));
                if (!Guid.TryParse(id, out _)) continue;
                await Frame.Write(stream, JsonSerializer.SerializeToUtf8Bytes(collector.Collect(id), ContractJson.Default.Snapshot), deadline.Token);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
            catch (Exception ex) when (ex is IOException or SocketException or InvalidDataException) { Console.Error.WriteLine("collector_ipc_failure"); }
        }
    }

    private static async Task Run(AgentConfig config, bool service, int limit, CancellationToken token)
    {
        LinuxDurability.PrivateDirectory(config.StateDirectory);
        using var spool = new DurableSpool(config);
        LinuxDurability.PrivateDirectory(Path.Combine(config.StateDirectory, "spool"));
        var checkpoint = new RiskCheckpointStore(config.StateDirectory);
        var engine = new RuleEngine(config, checkpoint.RestoreStates());
        await using var standalone = service ? null : new StandaloneCollector(config);
        var window = new List<Snapshot>();
        double lastExport = double.NegativeInfinity, lastInventory = double.NegativeInfinity;
        long lastDrops = spool.State.DroppedEvents;
        SystemdNotify.Send("READY=1");
        for (int sample = 0; !token.IsCancellationRequested && (limit <= 0 || sample < limit); sample++)
        {
            var watch = Stopwatch.StartNew();
            try
            {
                // Pending events retain their original timestamp. Advance
                // retention using wall time even while collection is paused.
                spool.PruneExpired(DateTimeOffset.UtcNow);
                if (!checkpoint.FlushPending(spool))
                {
                    Console.Error.WriteLine("risk_delivery_deferred:" + spool.LastError);
                    await HeartbeatWait.Delay(TimeSpan.FromSeconds(config.SampleSeconds), () => SystemdNotify.Send("WATCHDOG=1"), token);
                    continue;
                }
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                deadline.CancelAfter(TimeSpan.FromSeconds(Math.Max(8, config.SampleSeconds * 2)));
                Snapshot snapshot;
                if (service)
                {
                    using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(config.CollectorSocket), deadline.Token);
                    using var stream = new NetworkStream(socket);
                    await Frame.Write(stream, Encoding.UTF8.GetBytes(spool.State.AgentId), deadline.Token);
                    snapshot = JsonSerializer.Deserialize(await Frame.Read(stream, 16 * 1024 * 1024, deadline.Token), ContractJson.Default.Snapshot) ?? throw new InvalidDataException("snapshot");
                }
                else snapshot = await standalone!.Collect(spool.State.AgentId, deadline.Token);
                CapabilitySummary.Enrich(snapshot,config);
                var risk = engine.Evaluate(snapshot);
                window.Add(snapshot);
                if (window.Count > 300) window.RemoveAt(0);
                AgentHealth Health() => new() { CollectionDurationMs = watch.Elapsed.TotalMilliseconds, CollectorErrors = snapshot.Errors.Where(e => e.Status != "ok").ToList(), SpoolBytes = spool.SizeBytes, DroppedEvents = spool.State.DroppedEvents, RecoveredPartialWrites = spool.State.RecoveredPartialWrites, Status = snapshot.Errors.Any(e => e.Status != "ok") ? "degraded" : "ok" };
                TelemetryEvent CreateEvent(string type, Snapshot? data, List<RiskResult> risks)
                {
                    var e = new TelemetryEvent { EventType = type, AgentId = spool.State.AgentId, Sequence = spool.NextSequence(), ObservedAt = snapshot.ObservedAt, SampleDurationMs = data?.SampleDurationMs ?? snapshot.SampleDurationMs, Snapshot = data, Risk = risks, Health = Health() };
                    Validate(e);
                    return e;
                }
                bool Append(string type, Snapshot? data, List<RiskResult> risks)
                {
                    bool accepted = spool.Append(CreateEvent(type, data, risks));
                    if (!accepted) Console.Error.WriteLine("spool_loss:" + spool.LastError);
                    return accepted;
                }
                var statusEvent = new TelemetryEvent { EventType="health",AgentId=spool.State.AgentId,Sequence=Math.Max(1,spool.State.Sequence),ObservedAt=snapshot.ObservedAt,
                    Snapshot=new() { ObservedAt=snapshot.ObservedAt,MonotonicMs=snapshot.MonotonicMs,Host=snapshot.Host,Coverage=snapshot.Coverage },Health=Health() };
                AtomicFile.Write(Path.Combine(config.StateDirectory,"health.json"),JsonSerializer.SerializeToUtf8Bytes(statusEvent,ContractJson.Default.TelemetryEvent));
                var changes = risk.Where(r => r.LifecycleChanged).ToList();
                TelemetryEvent? transition = null;
                if (changes.Count > 0)
                {
                    // A short-lived process may disappear before the next ordinary summary.
                    // Persist its redacted context with the transition itself.
                    var ids = changes.Select(r => r.ProcessInstanceId).ToHashSet(StringComparer.Ordinal);
                    var context = JsonSerializer.Deserialize(JsonSerializer.SerializeToUtf8Bytes(snapshot, ContractJson.Default.Snapshot), ContractJson.Default.Snapshot)!;
                    context.Processes = context.Processes.Where(p => ids.Contains(p.ProcessInstanceId)).ToList();
                    context.Connections = context.Connections.Where(c => c.ProcessInstanceId is not null && ids.Contains(c.ProcessInstanceId)).ToList();
                    context.Persistence = context.Persistence.Where(p => p.ProcessInstanceId is not null && ids.Contains(p.ProcessInstanceId)).ToList();
                    transition = CreateEvent("risk", context, changes);
                }
                if (!checkpoint.Commit(engine.ExportState(), transition, spool))
                {
                    Console.Error.WriteLine("risk_delivery_deferred:" + spool.LastError);
                    await HeartbeatWait.Delay(TimeSpan.FromSeconds(config.SampleSeconds), () => SystemdNotify.Send("WATCHDOG=1"), token);
                    continue;
                }
                bool inventory = snapshot.MonotonicMs - lastInventory >= config.InventorySeconds * 1000;
                if (snapshot.MonotonicMs - lastExport >= config.ExportSeconds * 1000 || inventory)
                {
                    var summary = Summarize(window);
                    if (Append(inventory ? "inventory" : "telemetry", summary, risk.Where(r => r.Score > 0 || r.Lifecycle is "confirmed" or "suspected").ToList()))
                    { lastExport = snapshot.MonotonicMs; if (inventory) lastInventory = snapshot.MonotonicMs; window.Clear(); }
                }
                if (spool.State.DroppedEvents != lastDrops)
                { lastDrops = spool.State.DroppedEvents; Append("health", null, []); }
            }
            catch (Exception ex) when (ex is not RiskCheckpointException && ((ex is OperationCanceledException && !token.IsCancellationRequested) || ex is IOException or SocketException or JsonException))
            {
                // An IO failure can occur after Evaluate but before the atomic
                // commit. Restore the durable lifecycle before another sample.
                engine = new RuleEngine(config, checkpoint.RestoreStates());
                Console.Error.WriteLine("collection_unavailable:" + ex.GetType().Name);
                var health = new TelemetryEvent { EventType = "health", AgentId = spool.State.AgentId, Sequence = spool.NextSequence(), ObservedAt = DateTimeOffset.UtcNow,
                    Health = new() { Status = "degraded", CollectorErrors = [new("collector", "temporarily_unavailable", ex.GetType().Name)], SpoolBytes = spool.SizeBytes, DroppedEvents = spool.State.DroppedEvents } };
                spool.Append(health);
            }
            SystemdNotify.Send("WATCHDOG=1");
            if (limit > 0 && sample + 1 >= limit) break;
            await HeartbeatWait.Delay(TimeSpan.FromMilliseconds(Math.Max(1, config.SampleSeconds * 1000 - watch.Elapsed.TotalMilliseconds)), () => SystemdNotify.Send("WATCHDOG=1"), token);
        }
        spool.SaveState();
    }
    private static Snapshot Summarize(List<Snapshot> window)
    {
        var last = JsonSerializer.Deserialize(JsonSerializer.SerializeToUtf8Bytes(window[^1], ContractJson.Default.Snapshot), ContractJson.Default.Snapshot)!;
        var summary=new SummaryWindow();
        foreach(var sample in window)summary.Add(sample,sample.ObservedAt,sample.SampleDurationMs/1000);
        // Instantaneous measurements keep the same meaning on both OSes; aggregates have their own field.
        last.Summary=summary.Build();
        return last;
    }
}

internal sealed class StandaloneCollector : IAsyncDisposable
{
    private readonly Process child;
    private readonly Task initialized;
    public StandaloneCollector(AgentConfig config)
    {
        string path = Environment.ProcessPath ?? throw new InvalidOperationException("process_path");
        var info = new ProcessStartInfo(path) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(path) == "dotnet") info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "cryptoguard-agent.dll"));
        info.ArgumentList.Add("collect-stdio");
        child = Process.Start(info) ?? throw new InvalidOperationException("collector_start");
        _ = child.StandardError.ReadToEndAsync();
        initialized = Frame.Write(child.StandardInput.BaseStream, JsonSerializer.SerializeToUtf8Bytes(config, ContractJson.Default.AgentConfig), CancellationToken.None);
    }
    public async Task<Snapshot> Collect(string id, CancellationToken token)
    {
        await initialized.WaitAsync(token);
        await Frame.Write(child.StandardInput.BaseStream, Encoding.UTF8.GetBytes(id), token);
        return JsonSerializer.Deserialize(await Frame.Read(child.StandardOutput.BaseStream, 16 * 1024 * 1024, token), ContractJson.Default.Snapshot) ?? throw new InvalidDataException("snapshot");
    }
    public async ValueTask DisposeAsync()
    {
        child.StandardInput.Close();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try { await child.WaitForExitAsync(timeout.Token); } catch (OperationCanceledException) { }
        child.Dispose();
    }
}
