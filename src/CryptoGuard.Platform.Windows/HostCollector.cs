using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using CryptoGuard.Compatibility.Windows.Contracts;
using CryptoGuard.Compatibility.Windows.Core;

namespace CryptoGuard.Platform.Windows;

public sealed class HostCollector : IHostCollector
{
    private ulong? idleBefore, totalBefore;
    private readonly Dictionary<string, (long Rx, long Tx)> interfaces = new();
    private long stamp;
    public string BootId { get; } = GetBootId();
    public int CpuCount { get; } = Math.Max(1, (int)Native.GetActiveProcessorCount(ushort.MaxValue));
    private static string GetBootId()
    {
        var p = Marshal.AllocHGlobal(32);
        try { if (Native.NtQuerySystemInformation(90, p, 32, out _) == 0) return Marshal.PtrToStructure<Guid>(p).ToString("D"); }
        finally { Marshal.FreeHGlobal(p); }
        // Fallback explicitly identifies approximate boot time and cannot silently masquerade as an OS boot GUID.
        return "approx-" + (DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64)).ToUnixTimeSeconds() / 60;
    }
    public HostSample Collect()
    {
        double? cpu = null;
        if (Native.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            var total = kernel.Value + user.Value;
            if (totalBefore is ulong oldTotal && idleBefore is ulong oldIdle && total > oldTotal && idle.Value >= oldIdle)
                cpu = Math.Clamp(100d * (1 - (double)(idle.Value - oldIdle) / (total - oldTotal)), 0, 100);
            idleBefore = idle.Value; totalBefore = total;
        }
        var memory = new Native.MemoryStatus { Length = (uint)Marshal.SizeOf<Native.MemoryStatus>() };
        var hasMemory = Native.GlobalMemoryStatusEx(ref memory);
        var now = Stopwatch.GetTimestamp(); var elapsed = stamp == 0 ? 0 : Stopwatch.GetElapsedTime(stamp, now).TotalSeconds;
        long rx = 0, tx = 0; bool comparable = interfaces.Count > 0;
        var previousIds=interfaces.Keys.ToHashSet(StringComparer.Ordinal);
        var alive = new HashSet<string>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback))
        {
            try
            {
                var stats = nic.GetIPStatistics(); alive.Add(nic.Id);
                if (interfaces.TryGetValue(nic.Id, out var prev) && stats.BytesReceived >= prev.Rx && stats.BytesSent >= prev.Tx)
                { rx += stats.BytesReceived - prev.Rx; tx += stats.BytesSent - prev.Tx; }
                else comparable=false;
                interfaces[nic.Id] = (stats.BytesReceived, stats.BytesSent);
            }
            catch (NetworkInformationException) { comparable=false; }
        }
        comparable &= previousIds.SetEquals(alive) && elapsed > 0;
        foreach (var id in interfaces.Keys.Where(k => !alive.Contains(k)).ToArray()) interfaces.Remove(id);
        stamp = now;
        return new("windows", Environment.OSVersion.VersionString, RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(), BootId,
            Environment.MachineName, CpuCount, new(cpu, "percent_host_capacity", cpu is null ? "warmup" : "ok", "GetSystemTimes"),
            new(hasMemory ? 100d * (memory.TotalPhys - memory.AvailPhys) / memory.TotalPhys : null, "percent", hasMemory ? "ok" : "temporarily_unavailable", "GlobalMemoryStatusEx"),
            hasMemory ? (long)memory.TotalPhys : null,
            new(comparable && elapsed > 0 ? rx / elapsed : null, "bytes_per_second", comparable ? "ok" : "warmup", "NetworkInterface.host_aggregate"),
            new(comparable && elapsed > 0 ? tx / elapsed : null, "bytes_per_second", comparable ? "ok" : "warmup", "NetworkInterface.host_aggregate"));
    }
}
public sealed class IdleCollector : IIdleCollector
{
    public IdleSample Collect()
    {
        using var current = Process.GetCurrentProcess();
        if (current.SessionId == 0) return new(0, null, "unsupported", "session_0_requires_desktop_helper");
        var last = new Native.LastInput { Size = (uint)Marshal.SizeOf<Native.LastInput>() };
        return Native.GetLastInputInfo(ref last)
            ? new(current.SessionId, unchecked((uint)Environment.TickCount - last.Time) / 1000d, "ok", "GetLastInputInfo.calling_session_only")
            : new(current.SessionId, null, "temporarily_unavailable", "GetLastInputInfo");
    }
}
