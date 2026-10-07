namespace CryptoGuard.Core;

public static class Measurements
{
    public static double? Cpu(double? previousSeconds, double currentSeconds, double elapsedSeconds, int logicalCpus) =>
        previousSeconds is null || !double.IsFinite(currentSeconds) || !double.IsFinite(previousSeconds.Value) ||
        !double.IsFinite(elapsedSeconds) || elapsedSeconds <= 0 || logicalCpus < 1 || currentSeconds < previousSeconds ? null :
        Math.Clamp(100 * (currentSeconds - previousSeconds.Value) / (elapsedSeconds * logicalCpus), 0, 100);
    public static double? Rate(long? previousBytes, long currentBytes, double elapsedSeconds) =>
        previousBytes is null || !double.IsFinite(elapsedSeconds) || elapsedSeconds <= 0 || currentBytes < previousBytes ? null :
        (currentBytes - previousBytes.Value) / elapsedSeconds;
}
