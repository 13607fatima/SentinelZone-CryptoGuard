using CryptoGuard.Contracts;

namespace CryptoGuard.Core;

// Phase 23 boundary only: no HTTP, enrollment, token storage or production endpoint.
public sealed class LocalFakeReceiver : IEventSink
{
    public HashSet<string> AcceptedUids { get; } = [];
    public bool Acknowledge { get; set; }
    public ValueTask<bool> AcceptAsync(TelemetryEvent item, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); if (Acknowledge) AcceptedUids.Add(item.EventUid); return ValueTask.FromResult(Acknowledge); }
}
public static class Delivery
{
    public static TimeSpan RetryDelay(int attempt, double jitter)
        => TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, Math.Clamp(attempt, 0, 20))) * (0.75 + 0.5 * Math.Clamp(jitter, 0, 1)));
    public static async Task<int> TryDeliverAsync(ISpoolStore spool, IEventSink sink, CancellationToken token)
    {
        int count = 0;
        foreach (var item in spool.ReadAll().ToArray())
            if (await sink.AcceptAsync(item, token) && spool.Acknowledge(item.EventUid)) count++;
        return count;
    }
}
