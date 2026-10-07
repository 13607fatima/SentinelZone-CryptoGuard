using CryptoGuard.Contracts;
using CryptoGuard.Core;
using CryptoGuard.Platform.Linux;
using System.Text.Json;

internal static class Regression0221
{
    private static void Check(bool ok) { if (!ok) throw new Exception("regression assertion"); }
    private static readonly DateTimeOffset Epoch = new(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
    private static AgentConfig Config() => new() { StateDirectory = Path.Combine(Path.GetTempPath(), "cg-0221-" + Guid.NewGuid()), SpoolLimitBytes = 1048576, RiskReserveBytes = 131072 };
    private static TelemetryEvent Event(DurableSpool spool, string type = "risk", int padding = 0) => new()
    {
        AgentId = spool.State.AgentId, Sequence = spool.NextSequence(), EventType = type, ObservedAt = Epoch,
        Snapshot = new() { Host = new() { OsVersion = new string('x', padding) } }
    };
    private static List<RiskState> States(string lifecycle) => [new() { InstanceId = "instance", BootId = "boot", Lifecycle = lifecycle, AlertId = "alert", CpuHistory = [1, 2] }];
    private static void DeferredTransition(string lifecycle)
    {
        var config = Config();
        string uid;
        byte[] pendingBytes;
        using (var spool = new DurableSpool(config))
        {
            for (int i = 0; i < 100; i++) if (!spool.Append(Event(spool, "health", 65536))) break;
            var transition = Event(spool, padding: 131072);
            transition.Risk.Add(new() { ProcessInstanceId = "instance", Lifecycle = lifecycle, LifecycleChanged = true });
            uid = transition.EventUid;
            var journal = new RiskCheckpointStore(config.StateDirectory);
            Check(!journal.Commit(lifecycle == "resolved" ? [] : States(lifecycle), transition, spool));
            Check(journal.HasPendingEvent);
            pendingBytes = File.ReadAllBytes(Path.Combine(config.StateDirectory, "risk-checkpoint.json"));
        }
        using (var spool = new DurableSpool(config))
        {
            var journal = new RiskCheckpointStore(config.StateDirectory);
            Check(journal.HasPendingEvent && !journal.FlushPending(spool));
            foreach (var old in spool.ReadAll().ToArray()) spool.Acknowledge(old.EventUid);
            Check(journal.FlushPending(spool) && !journal.HasPendingEvent);
            Check(spool.ReadAll().Single().EventUid == uid && spool.ReadAll().Single().Risk.Single().Lifecycle == lifecycle);
            // Crash after durable append, before checkpoint-clear became durable.
            AtomicFile.Write(Path.Combine(config.StateDirectory, "risk-checkpoint.json"), pendingBytes);
            var recovered = new RiskCheckpointStore(config.StateDirectory);
            Check(recovered.FlushPending(spool));
            Check(spool.ReadAll().Count() == 1 && spool.ReadAll().Single().EventUid == uid);
        }
        Directory.Delete(config.StateDirectory, true);
    }
    public static void Run(Action<string, Action> test)
    {
        test("cron system command keeps equals arguments", () => Check(LinuxCollector.CronCommand("*/5 * * * * root /opt/worker --threads=2 --algo=rx/0", false) == "/opt/worker --threads=2 --algo=rx/0"));
        test("cron per-user and reboot keep equals arguments", () =>
        {
            Check(LinuxCollector.CronCommand("* * * * * /opt/worker --mode=x", true) == "/opt/worker --mode=x");
            Check(LinuxCollector.CronCommand("@reboot root /opt/worker --mode=x", false) == "/opt/worker --mode=x");
            Check(LinuxCollector.CronCommand("@reboot /opt/worker --mode=x", true) == "/opt/worker --mode=x");
        });
        test("cron environment and comments are not commands", () =>
        {
            foreach (string line in new[] { "SHELL=/bin/sh", " MAILTO = a=b ", "# note=x", " ", "* *" }) Check(LinuxCollector.CronCommand(line, true) is null);
        });
        test("cron preserves quoted command whitespace", () => Check(LinuxCollector.FirstExecutable(LinuxCollector.CronCommand("* * * * * root \"/opt/a  b/worker\" --value=x", false)!) == "/opt/a  b/worker"));
        test("deferred confirmed transition survives quota and restart", () => DeferredTransition("confirmed"));
        test("deferred resolved transition survives quota and restart", () => DeferredTransition("resolved"));
        test("pending transition cannot be overwritten", () =>
        {
            var config = Config(); using var spool = new DurableSpool(config);
            var journal = new RiskCheckpointStore(config.StateDirectory);
            var rejecting = new RejectingSpool();
            Check(!journal.Commit(States("confirmed"), Event(spool), rejecting));
            bool rejected = false;
            try { journal.Commit([], null, rejecting); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && journal.HasPendingEvent);
        });
        test("checkpoint restoration does not alias live engine state", () =>
        {
            var config = Config(); using var spool = new DurableSpool(config);
            var journal = new RiskCheckpointStore(config.StateDirectory);
            var states = States("confirmed"); Check(journal.Commit(states, null, spool));
            states[0].Lifecycle = "resolved"; states[0].CpuHistory.Clear();
            var restored = journal.RestoreStates(); restored[0].CpuHistory.Clear();
            Check(journal.RestoreStates()[0].Lifecycle == "confirmed" && journal.RestoreStates()[0].CpuHistory.Count == 2);
        });
        test("checkpoint IO failure stops commit before spool append", () =>
        {
            var config = Config(); using var spool = new DurableSpool(config);
            var journal = new RiskCheckpointStore(config.StateDirectory);
            var rejecting = new RejectingSpool(); var item = Event(spool);
            var previous = AtomicFile.SyncDirectory;
            AtomicFile.SyncDirectory = _ => throw new IOException("checkpoint_fault");
            bool failed = false;
            try { journal.Commit(States("confirmed"), item, rejecting); } catch (RiskCheckpointException) { failed = true; }
            finally { AtomicFile.SyncDirectory = previous; }
            Check(failed && rejecting.Attempts == 0);
        });
        test("legacy risk state migrates without changing lifecycle", () =>
        {
            var config = Config(); using var spool = new DurableSpool(config);
            AtomicFile.Write(Path.Combine(config.StateDirectory, "risk-state.json"), JsonSerializer.SerializeToUtf8Bytes(States("confirmed"), ContractJson.Default.ListRiskState));
            var journal = new RiskCheckpointStore(config.StateDirectory);
            Check(journal.RestoreStates()[0].Lifecycle == "confirmed");
            Check(journal.Commit(journal.RestoreStates(), null, spool));
            Check(new RiskCheckpointStore(config.StateDirectory).RestoreStates()[0].Lifecycle == "confirmed");
        });
        test("directory fsync retry is required before duplicate ACK", () =>
        {
            var config = Config(); using var spool = new DurableSpool(config); var item = Event(spool);
            var previous = AtomicFile.SyncDirectory;
            AtomicFile.SyncDirectory = p => { if (p.EndsWith("spool", StringComparison.Ordinal)) throw new IOException("fsync_fault"); previous(p); };
            try { Check(!spool.Append(item) && !spool.Append(item)); }
            finally { AtomicFile.SyncDirectory = previous; }
            Check(spool.Append(item) && spool.ReadAll().Count() == 1);
        });
        test("indexed spool bytes survive ACK and recovery", () =>
        {
            var config = Config(); long bytes;
            using (var spool = new DurableSpool(config))
            {
                for (int i = 0; i < 32; i++) Check(spool.Append(Event(spool, "telemetry", 1024)));
                var first = spool.ReadAll().First(); Check(spool.Acknowledge(first.EventUid));
                bytes = Directory.GetFiles(Path.Combine(config.StateDirectory, "spool"), "*.event").Sum(p => new FileInfo(p).Length);
                Check(bytes == spool.SizeBytes && !spool.Acknowledge(first.EventUid));
            }
            using (var recovered = new DurableSpool(config)) Check(recovered.SizeBytes == bytes && recovered.ReadAll().Count() == 31);
            Directory.Delete(config.StateDirectory, true);
        });
        test("retention handles non-sequential observed times", () =>
        {
            var config = Config(); config.RetentionHours = 1; using var spool = new DurableSpool(config);
            var recent = Event(spool); recent.ObservedAt = Epoch.AddMinutes(30); Check(spool.Append(recent));
            var old = Event(spool); Check(spool.Append(old));
            var future = Event(spool); future.ObservedAt = Epoch.AddMinutes(70); Check(spool.Append(future));
            Check(spool.ReadAll().Count() == 2 && spool.ReadAll().All(e => e.EventUid != old.EventUid));
        });
        test("intentional idle delay emits repeated watchdog heartbeats", () =>
        {
            int count = 0;
            HeartbeatWait.Delay(TimeSpan.FromMilliseconds(120), () => count++, default, TimeSpan.FromMilliseconds(15)).GetAwaiter().GetResult();
            Check(count >= 2);
        });
        test("retention frees a blocked outbox without new collection", () =>
        {
            var config = Config(); config.RetentionHours = 1; using var spool = new DurableSpool(config);
            for (int i = 0; i < 100; i++) if (!spool.Append(Event(spool, "health", 65536))) break;
            var journal = new RiskCheckpointStore(config.StateDirectory);
            var pending = Event(spool, padding: 131072);
            Check(!journal.Commit(States("confirmed"), pending, spool));
            spool.PruneExpired(Epoch.AddHours(2));
            Check(journal.FlushPending(spool) && spool.ReadAll().Single().EventUid == pending.EventUid);
        });
        test("heartbeat delay cancels promptly", () =>
        {
            using var stop = new CancellationTokenSource(); stop.Cancel(); bool canceled = false;
            try { HeartbeatWait.Delay(TimeSpan.FromSeconds(300), () => { }, stop.Token).GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { canceled = true; }
            Check(canceled);
        });
    }
    private sealed class RejectingSpool : ISpoolStore
    {
        public int Attempts { get; private set; }
        public long SizeBytes => 0;
        public bool Append(TelemetryEvent item) { Attempts++; return false; }
        public IEnumerable<TelemetryEvent> ReadAll() => [];
        public bool Acknowledge(string eventUid) => false;
    }
}
