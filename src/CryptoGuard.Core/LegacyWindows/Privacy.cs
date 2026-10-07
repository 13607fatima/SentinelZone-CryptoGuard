using System.Text.RegularExpressions;

namespace CryptoGuard.Compatibility.Windows.Core;

// Persist semantic flags only. Raw command lines, URLs, connection strings and arbitrary arguments never leave the collector.
public static partial class Privacy
{
    public static string[] ArgumentFeatures(string? commandLine)
    {
        if (string.IsNullOrEmpty(commandLine)) return [];
        if (commandLine.Length > 32768) commandLine = commandLine[..32768];
        var result = new List<string>();
        if (Stratum().IsMatch(commandLine)) result.Add("stratum_protocol");
        if (Algorithm().IsMatch(commandLine)) result.Add("mining_algorithm");
        if (Pool().IsMatch(commandLine)) result.Add("pool_option");
        if (Wallet().IsMatch(commandLine)) result.Add("wallet_option");
        return result.ToArray();
    }
    [GeneratedRegex(@"\bstratum\+(tcp|ssl|tls)://", RegexOptions.IgnoreCase, 50)] private static partial Regex Stratum();
    [GeneratedRegex(@"(?:^|\s)(?:--algo(?:=|\s+)|-a\s+)[""']?(?:rx(?:/0)?|randomx|ethash|kawpow|cryptonight)(?:[""']?\s|$)", RegexOptions.IgnoreCase, 50)] private static partial Regex Algorithm();
    [GeneratedRegex(@"(?:^|\s)(?:--url|--pool|--pool-address|-o)(?:=|\s)", RegexOptions.IgnoreCase, 50)] private static partial Regex Pool();
    [GeneratedRegex(@"(?:^|\s)(?:--wallet|--user|-u)(?:=|\s)", RegexOptions.IgnoreCase, 50)] private static partial Regex Wallet();
    public static string? ExecutableFromCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        command = command.Trim();
        string path;
        if (command.StartsWith('"')) { var end = command.IndexOf('"', 1); if (end < 0) return null; path = command[1..end]; }
        else { var end = command.IndexOf(' '); path = end < 0 ? command : command[..end]; }
        path = Environment.ExpandEnvironmentVariables(path);
        return Path.IsPathFullyQualified(path) && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? path : null;
    }
}
