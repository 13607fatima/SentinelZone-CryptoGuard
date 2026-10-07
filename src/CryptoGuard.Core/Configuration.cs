using CryptoGuard.Contracts;
using System.Text.Json;
using System.Net;

namespace CryptoGuard.Core;

public static class Configuration
{
    public static AgentConfig Load(string? path)
    {
        var c = path is null ? new AgentConfig() : JsonSerializer.Deserialize(File.ReadAllText(path), ContractJson.Default.AgentConfig) ?? throw new InvalidDataException("invalid_config");
        Validate(c);
        return c;
    }
    public static void Validate(AgentConfig c, bool validatePaths = true)
    {
        if (c.SampleSeconds is < 1 or > 300 || c.NetworkSeconds < c.SampleSeconds || c.SensorSeconds < c.SampleSeconds || c.ExportSeconds < c.SampleSeconds || c.InventorySeconds < c.SampleSeconds || c.PersistenceSeconds < c.SampleSeconds)
            throw new InvalidDataException("invalid_intervals");
        if (c.SpoolLimitBytes < 1048576 || c.RiskReserveBytes < 65536 || c.RiskReserveBytes >= c.SpoolLimitBytes || c.RetentionHours is < 1 or > 8760 || c.MaxProcesses is < 1 or > 65536)
            throw new InvalidDataException("invalid_limits");
        if (c.ConfirmSeconds is < 1 or > 86400 || c.ResolveSeconds is < 1 or > 86400 || c.HighCpuPercentHostCapacity is <= 0 or > 100 || c.MiningAuthorization is not ("unknown" or "prohibited"))
            throw new InvalidDataException("invalid_policy");
        if (validatePaths && (!Path.IsPathFullyQualified(c.StateDirectory) || !Path.IsPathFullyQualified(c.CollectorSocket))) throw new InvalidDataException("absolute_paths_required");
        foreach (var rule in c.AuthorizedWorkloads)
            if (rule.Sha256.Length != 64 || !rule.Sha256.All(Uri.IsHexDigit) || string.IsNullOrWhiteSpace(rule.ExePath) || validatePaths && !Path.IsPathFullyQualified(rule.ExePath) || string.IsNullOrWhiteSpace(rule.PolicyId)) throw new InvalidDataException("allowlist_requires_hash_path_policy");
        foreach (var ioc in c.Iocs)
        {
            if (ioc.Type is not ("ip" or "sha256") || ioc.Confidence is < 0 or > 1 || string.IsNullOrWhiteSpace(ioc.Source) || string.IsNullOrWhiteSpace(ioc.Value)) throw new InvalidDataException("invalid_ioc");
            if (ioc.Type == "ip") { if (!IPAddress.TryParse(ioc.Value, out var address)) throw new InvalidDataException("invalid_ip_ioc"); ioc.Value = address.ToString(); }
            if (ioc.Type == "sha256" && (ioc.Value.Length != 64 || !ioc.Value.All(Uri.IsHexDigit))) throw new InvalidDataException("invalid_hash_ioc");
        }
    }
}
