using System.Globalization;
using System.Net;

namespace CryptoGuard.Platform.Linux;

public sealed record ParsedProcess(int Pid, int Ppid, ulong CpuTicks, ulong StartTicks, long RssPages);
public static class ProcParsing
{
    public static ParsedProcess ParseProcessStat(string text)
    {
        int open = text.IndexOf('('), end = text.LastIndexOf(')');
        if (open <= 0 || end <= open) throw new FormatException("proc_stat_shape");
        var fields = text[(end + 1)..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 22) throw new FormatException("proc_stat_short");
        return new(int.Parse(text[..open].Trim(), CultureInfo.InvariantCulture), int.Parse(fields[1], CultureInfo.InvariantCulture),
            checked(ulong.Parse(fields[11], CultureInfo.InvariantCulture) + ulong.Parse(fields[12], CultureInfo.InvariantCulture)),
            ulong.Parse(fields[19], CultureInfo.InvariantCulture), long.Parse(fields[21], CultureInfo.InvariantCulture));
    }
    public static (string Address, int Port) ParseEndpoint(string value)
    {
        var pair = value.Split(':');
        if (pair.Length != 2 || pair[0].Length is not (8 or 32)) throw new FormatException("proc_endpoint");
        byte[] bytes = Convert.FromHexString(pair[0]);
        // proc TCP renders each 32-bit word in host byte order, including IPv6.
        if (BitConverter.IsLittleEndian) for (int i = 0; i < bytes.Length; i += 4) Array.Reverse(bytes, i, 4);
        return (new IPAddress(bytes).ToString(), int.Parse(pair[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }
    public static double? CpuPercent(ulong current, ulong previous, double elapsedSeconds, long clockTicks, int logicalCpus)
        => current < previous || elapsedSeconds <= 0 || clockTicks <= 0 || logicalCpus <= 0 ? null : Math.Clamp(100.0 * (current - previous) / clockTicks / elapsedSeconds / logicalCpus, 0, 100);
}
