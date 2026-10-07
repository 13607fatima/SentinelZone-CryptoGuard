using System.Diagnostics;

namespace CryptoGuard.Core;

public static class HeartbeatWait
{
    // Heartbeats run only during intentional idle waits. A stuck collection or
    // persistence operation must still be detected by the service watchdog.
    public static async Task Delay(TimeSpan duration, Action heartbeat, CancellationToken token,
        TimeSpan? interval = null)
    {
        TimeSpan period = interval ?? TimeSpan.FromSeconds(10);
        if (period <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
        var watch = Stopwatch.StartNew();
        while (duration - watch.Elapsed is var remaining && remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining < period ? remaining : period, token);
            heartbeat();
        }
    }
}
