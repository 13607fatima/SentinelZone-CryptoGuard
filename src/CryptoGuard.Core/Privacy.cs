using System.Text.RegularExpressions;

namespace CryptoGuard.Core;

public static partial class Privacy
{
    private static readonly HashSet<string> SafeFlags = new(StringComparer.OrdinalIgnoreCase)
    { "--algo", "-a", "--coin", "--url", "-o", "--user", "-u", "--pass", "-p", "--password", "--token", "--threads", "-t", "--donate-level", "--tls", "--background", "--randomx-mode", "--cpu-priority" };
    private static readonly HashSet<string> MiningAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    { "rx/0", "rx/wow", "randomx", "cryptonight", "cn/r", "kawpow", "ethash", "etchash" };

    // Values are discarded rather than trying to enumerate every possible secret format.
    // No raw command line is ever returned or logged, including unknown flags or positional arguments.
    public static (string[] Arguments, bool MiningCombination) SummarizeArguments(IEnumerable<string> source)
    {
        var args = source.Take(256).ToArray();
        var safe = new List<string>();
        bool algorithm = false, endpoint = false, user = false;
        for (int i = 0; i < args.Length; i++)
        {
            var token = args[i];
            int split = token.IndexOf('=');
            string flag = split < 0 ? token : token[..split];
            string value = split < 0 ? (i + 1 < args.Length ? args[i + 1] : "") : token[(split + 1)..];
            if (flag is "--algo" or "-a") algorithm |= MiningAlgorithms.Contains(value);
            if (flag == "--coin") algorithm |= value.Equals("monero", StringComparison.OrdinalIgnoreCase);
            if (flag is "--url" or "-o") endpoint |= value.StartsWith("stratum+tcp://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("stratum+ssl://", StringComparison.OrdinalIgnoreCase);
            if (flag is "--user" or "-u") user |= value.Length > 0;
            safe.Add(SafeFlags.Contains(flag) ? flag.ToLowerInvariant() + (split >= 0 ? "=[redacted]" : "") : "[redacted]");
        }
        return (safe.Distinct().Take(32).ToArray(), (algorithm && endpoint) || (endpoint && user));
    }

    public static string PathLabel(string path)
    {
        if (path.StartsWith("/home/", StringComparison.Ordinal))
        {
            int end = path.IndexOf('/', 6);
            return end < 0 ? "/home/[user]" : "/home/[user]" + path[end..];
        }
        return path;
    }
}
