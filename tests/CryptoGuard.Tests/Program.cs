using CryptoGuard.Contracts;
using CryptoGuard.Core;
using CryptoGuard.Platform.Linux;
using CryptoGuard.Risk;
using System.Runtime.Versioning;
using System.Text.Json;

[assembly: SupportedOSPlatform("linux")]

internal static class Program
{
    private static int passed, failed;
    private static readonly DateTimeOffset Epoch = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);
    private static void Assert(bool value, string detail = "assertion") { if (!value) throw new Exception(detail); }
    private static void Test(string name, Action test)
    { try { test(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + " " + e.GetType().Name + " " + e.Message); } }
    private static AgentConfig Config(string? state = null) => new() { StateDirectory = state ?? Path.Combine(Path.GetTempPath(), "cg-test-" + Guid.NewGuid()), SampleSeconds = 5, SpoolLimitBytes = 1048576, RiskReserveBytes = 131072 };
    private static Snapshot Sample(double seconds, bool args = false, double? cpu = 80, string id = "instance-a") => new()
    {
        ObservedAt = Epoch.AddSeconds(seconds), MonotonicMs = 100000 + seconds * 1000, SampleDurationMs = 5000,
        Host = new() { BootId = "boot-a" }, Processes = [new() { ProcessInstanceId = id, Pid = 123, StartTicks = 100, ExePath = "/tmp/worker", UnusualWritableLocation = true, MiningArgumentCombination = args,
            CpuPercentHostCapacity = cpu is double value ? Metric.Ok(value, "percent_host_capacity", "fixture") : Metric.Missing("percent_host_capacity", "fixture", "warm_up") }]
    };
    private static TelemetryEvent Event(DurableSpool spool, string type = "telemetry") => new() { AgentId = spool.State.AgentId, Sequence = spool.NextSequence(), EventType = type, ObservedAt = Epoch, Snapshot = Sample(0) };
    public static int Main(string[] args)
    {
        if (OperatingSystem.IsLinux()) LinuxDurability.Initialize();
        Test("CPU multi-core normalization", () => Assert(ProcParsing.CpuPercent(900, 100, 2, 100, 4) == 100));
        Test("CPU one full core on four CPU host", () => Assert(ProcParsing.CpuPercent(300, 100, 2, 100, 4) == 25));
        Test("CPU reset produces unknown", () => Assert(ProcParsing.CpuPercent(10, 20, 1, 100, 4) is null));
        Test("CPU invalid elapsed produces unknown", () => Assert(ProcParsing.CpuPercent(20, 10, 0, 100, 4) is null));
        Test("proc stat embedded parentheses", () =>
        {
            string text = "42 (odd ) name) R 7 0 0 0 0 0 0 0 0 0 101 202 0 0 0 0 0 0 909 0 12";
            var p = ProcParsing.ParseProcessStat(text); Assert(p.Pid == 42 && p.Ppid == 7 && p.CpuTicks == 303 && p.StartTicks == 909 && p.RssPages == 12);
        });
        Test("TCP IPv4 endian and port", () => Assert(ProcParsing.ParseEndpoint("0100007F:0D05") == ("127.0.0.1", 3333)));
        Test("TCP IPv6 word endian", () => Assert(ProcParsing.ParseEndpoint("00000000000000000000000001000000:01BB") == ("::1", 443)));
        Test("quoted persistence executable", () => Assert(LinuxCollector.FirstExecutable("\"/opt/a b/worker\" --password secret") == "/opt/a b/worker"));
        Test("persistence shell expression never executed", () => Assert(LinuxCollector.FirstExecutable("$(touch /tmp/never)") is null));
        foreach (string secret in new[] { "--password=super-secret", "--token", "postgres://user:secret@host/db", "Password=secret;User ID=admin", "Bearer secret", "--unknown-secret=secret", "\"quoted secret\"" })
            Test("privacy " + secret.Split('=')[0].Split(' ')[0], () => Assert(!string.Join(' ', Privacy.SummarizeArguments([secret, "secret"]).Arguments).Contains("secret", StringComparison.Ordinal)));
        Test("mining args combination", () => Assert(Privacy.SummarizeArguments(["--algo=rx/0", "--url=stratum+tcp://user:secret@pool:3333"]).MiningCombination));
        Test("single mining word insufficient", () => Assert(!Privacy.SummarizeArguments(["xmrig", "--threads=8"]).MiningCombination));
        Test("all normal compute scenarios stay low", () =>
        {
            foreach (string workload in new[] { "cpu_stress", "ram_stress", "compile", "browser_video", "game", "video_encode", "backup", "ai" })
            { var r = new RuleEngine(Config()); for (int t = 0; t <= 180; t += 5) Assert(r.Evaluate(Sample(t, false, 99, workload))[0].Severity == "low"); }
        });
        Test("warmup unknown is not zero or threat", () => Assert(new RuleEngine(Config()).Evaluate(Sample(0, false, null))[0].Score == 0));
        Test("port-only match insufficient", () => { var s = Sample(0); s.Connections.Add(new("n", "ipv4", "127.0.0.1", 3333, "1.2.3.4", 3333, "established", "1", "instance-a", "ok")); Assert(new RuleEngine(Config()).Evaluate(s)[0].Score == 0); });
        Test("offline renamed miner real-time lifecycle", () =>
        {
            var r = new RuleEngine(Config()); RiskResult? last = null;
            for (int t = 0; t <= 90; t += 5) { last = r.Evaluate(Sample(t, true))[0]; if (t < 90) Assert(last.Lifecycle != "confirmed"); }
            Assert(last!.Lifecycle == "confirmed" && last.Severity == "high" && last.Authorization == "unknown");
        });
        Test("rapid scans cannot shortcut confirmation", () => { var r = new RuleEngine(Config()); for (int t = 0; t < 100; t++) Assert(r.Evaluate(Sample(t * .001, true))[0].Lifecycle != "confirmed"); });
        Test("sampling gap resets sustained suspicion", () => { var r = new RuleEngine(Config()); for (int t = 0; t <= 60; t += 5) r.Evaluate(Sample(t, true)); Assert(r.Evaluate(Sample(300, true))[0].Lifecycle != "confirmed"); });
        Test("PID reuse does not inherit history", () => { var r = new RuleEngine(Config()); for (int t = 0; t <= 90; t += 5) r.Evaluate(Sample(t, true)); var s = Sample(95, true, 80, "instance-new"); s.Processes[0].StartTicks = 999; Assert(r.Evaluate(s).First(x => x.ProcessInstanceId == "instance-new").Lifecycle != "confirmed"); });
        Test("alert resolves using elapsed time", () => { var r = new RuleEngine(Config()); for (int t = 0; t <= 90; t += 5) r.Evaluate(Sample(t, true)); RiskResult? last = null; for (int t = 95; t <= 155; t += 5) last = r.Evaluate(Sample(t, false, 0))[0]; Assert(last!.Lifecycle == "resolved"); });
        Test("missing process resolves alert", () => { var r = new RuleEngine(Config()); for (int t = 0; t <= 90; t += 5) r.Evaluate(Sample(t, true)); RiskResult? last=null; for(int t=95;t<=155;t+=5) { var s=Sample(t); s.Processes.Clear(); var result=r.Evaluate(s); if(result.Count>0)last=result.Single(); } Assert(last?.Lifecycle == "resolved"); });
        Test("trusted malicious hash separate high rule", () => { var c = Config(); c.Iocs.Add(new() { Type = "sha256", Value = new string('a', 64), Source = "test", Confidence = .99, ExpiresAt = Epoch.AddDays(1) }); var s = Sample(0, false, 0); s.Processes[0].Sha256 = new string('a', 64); Assert(new RuleEngine(c).Evaluate(s)[0].Severity == "critical"); });
        Test("expired IOC is ignored", () => { var c = Config(); c.Iocs.Add(new() { Type = "sha256", Value = new string('a', 64), Source = "test", Confidence = 1, ExpiresAt = Epoch.AddDays(-1) }); var s = Sample(0, false, 0); s.Processes[0].Sha256 = new string('a', 64); Assert(new RuleEngine(c).Evaluate(s)[0].Score == 0); });
        Test("low CPU plus trusted process IOC still scores", () => { var c = Config(); c.Iocs.Add(new() { Value = "192.0.2.1", Source = "test", Confidence = 1, ExpiresAt = Epoch.AddDays(1) }); var s = Sample(0, true, 1); s.Connections.Add(new("n", "ipv4", "127.0.0.1", 4444, "192.0.2.1", 443, "established", "1", "instance-a", "ok")); Assert(new RuleEngine(c).Evaluate(s)[0].Severity == "high"); });
        Test("allowlist requires exact hash and path", () => { var c = Config(); c.AuthorizedWorkloads.Add(new() { Sha256 = new string('a', 64), ExePath = "/tmp/worker", PolicyId = "approved-lab" }); var s = Sample(0, true); s.Processes[0].Sha256 = new string('a', 64); Assert(new RuleEngine(c).Evaluate(s)[0].Authorization == "authorized"); s.Processes[0].ExePath = "/tmp/imposter"; Assert(new RuleEngine(c).Evaluate(s)[0].Authorization == "unknown"); });
        Test("package trust never erases evidence", () => { var s = Sample(0, true); s.Processes[0].FileTrust = new("debian_package", "ok", "safe-package", "matches_local_package_metadata"); Assert(new RuleEngine(Config()).Evaluate(s)[0].Score == 35); });
        Test("persistent lifecycle replay checkpoint", () => { var c = Config(); var r = new RuleEngine(c); for (int t = 0; t <= 60; t += 5) r.Evaluate(Sample(t, true)); var restored = new RuleEngine(c, JsonSerializer.Deserialize(JsonSerializer.Serialize(r.ExportState(), ContractJson.Default.ListRiskState), ContractJson.Default.ListRiskState)); for (int t = 65; t <= 90; t += 5) Assert(JsonSerializer.Serialize(r.Evaluate(Sample(t, true)), ContractJson.Default.ListRiskResult) == JsonSerializer.Serialize(restored.Evaluate(Sample(t, true)), ContractJson.Default.ListRiskResult)); });
        Test("identity survives reopen and unique fresh install", () => { var c = Config(); string id; using (var s = new DurableSpool(c)) id = s.State.AgentId; using (var s = new DurableSpool(c)) Assert(s.State.AgentId == id); using (var s = new DurableSpool(Config())) Assert(s.State.AgentId != id); });
        Test("spool exclusive writer", () => { var c = Config(); using var s = new DurableSpool(c); bool rejected = false; try { using var duplicate = new DurableSpool(c); } catch (IOException) { rejected = true; } Assert(rejected); });
        Test("unacknowledged retry preserves event uid", () => { using var s = new DurableSpool(Config()); var e = Event(s); Assert(s.Append(e)); var sink = new LocalFakeReceiver(); Delivery.TryDeliverAsync(s, sink, default).GetAwaiter().GetResult(); Assert(s.ReadAll().Single().EventUid == e.EventUid); sink.Acknowledge = true; Assert(Delivery.TryDeliverAsync(s, sink, default).GetAwaiter().GetResult() == 1 && !s.ReadAll().Any()); });
        Test("duplicate durable append idempotent", () => { using var s = new DurableSpool(Config()); var e = Event(s); Assert(s.Append(e) && s.Append(e) && s.ReadAll().Count() == 1); });
        Test("partial crash write recovered with visible loss", () => { var c = Config(); using (var s = new DurableSpool(c)) File.WriteAllText(Path.Combine(c.StateDirectory, "spool", "partial.pending"), "{broken"); using var recovered = new DurableSpool(c); Assert(recovered.State.RecoveredPartialWrites == 1 && recovered.State.DroppedEvents == 1); });
        Test("corrupt committed write detected", () => { var c = Config(); using (var s = new DurableSpool(c)) { s.Append(Event(s)); File.AppendAllText(Directory.GetFiles(Path.Combine(c.StateDirectory, "spool"), "*.event").Single(), "bad"); } using var recovered = new DurableSpool(c); Assert(recovered.State.DroppedEvents == 1 && !recovered.ReadAll().Any()); });
        Test("risk reserve and explicit quota loss", () => { using var s = new DurableSpool(Config()); bool rejected = false; for (int i = 0; i < 500; i++) { var e = Event(s); e.Snapshot!.Host.OsVersion = new string('x', 10000); if (!s.Append(e)) { rejected = true; break; } } Assert(rejected && s.State.DroppedEvents > 0); Assert(s.Append(Event(s, "risk"))); });
        Test("retention loss explicitly counted", () => { var c = Config(); c.RetentionHours = 1; using var s = new DurableSpool(c); s.Append(Event(s)); var future = Event(s); future.ObservedAt = Epoch.AddHours(2); Assert(s.Append(future) && s.State.DroppedEvents == 1 && s.ReadAll().Count() == 1); });
        Test("backoff exponential bounded and jittered", () => Assert(Delivery.RetryDelay(3, 0) < Delivery.RetryDelay(3, 1) && Delivery.RetryDelay(50, 1).TotalSeconds <= 375));
        Test("source generated JSON round trip", () => { var sample = Sample(0, false, null); var json = JsonSerializer.Serialize(sample, ContractJson.Default.Snapshot); var read = JsonSerializer.Deserialize(json, ContractJson.Default.Snapshot)!; Assert(read.Processes[0].CpuPercentHostCapacity.Value is null && read.Processes[0].CpuPercentHostCapacity.Status == "warm_up" && read.ObservedAt.Offset == TimeSpan.Zero); });
        Test("configuration rejects unsafe allowlist", () => { var c = Config(); c.AuthorizedWorkloads.Add(new() { ExePath = "/tmp/worker" }); bool rejected = false; try { Configuration.Validate(c); } catch (InvalidDataException) { rejected = true; } Assert(rejected); });
        Test("IPv6 IOC canonicalization", () => { var c = Config(); c.Iocs.Add(new() { Value = "0:0:0:0:0:0:0:1", Source = "test", Confidence = 1, ExpiresAt = Epoch.AddDays(1) }); Configuration.Validate(c); Assert(c.Iocs[0].Value == "::1"); });
        Test("disabled persistence does not score", () => { var s = Sample(0, true); s.Persistence.Add(new("xdg", "fixture.desktop", "/tmp/worker", "instance-a", "disabled")); Assert(new RuleEngine(Config()).Evaluate(s)[0].Score == 35); });
        Test("IO failure is not acknowledged as durable", () => { using var s = new DurableSpool(Config()); var previous = AtomicFile.SyncDirectory; AtomicFile.SyncDirectory = p => { if (p.EndsWith("spool", StringComparison.Ordinal)) throw new IOException("injected_fsync_failure"); previous(p); }; try { Assert(!s.Append(Event(s)) && s.State.DroppedEvents == 1); } finally { AtomicFile.SyncDirectory = previous; } });
        Regression0221.Run(Test);
        if (args.Length > 0)
        {
            Directory.CreateDirectory(args[0]);
            using var writer = new StreamWriter(Path.Combine(args[0], "replay-fixture.jsonl"));
            for (int t = 0; t <= 180; t += 5)
            {
                var e = new TelemetryEvent { AgentId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", EventUid = new Guid(t + 1, 0, 0, new byte[8]).ToString(), Sequence = t + 1, ObservedAt = Epoch.AddSeconds(t), Snapshot = Sample(t, t <= 100, t <= 100 ? 80 : 0) };
                writer.WriteLine(JsonSerializer.Serialize(e, ContractJson.Default.TelemetryEvent));
            }
        }
        Console.WriteLine($"RESULT passed={passed} failed={failed}");
        return failed == 0 ? 0 : 1;
    }
}
