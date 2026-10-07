using CryptoGuard.Contracts;

namespace CryptoGuard.Platform.Linux;

public sealed partial class LinuxCollector
{
    public List<Connection> CollectConnections(IReadOnlyList<ProcessSample> processes)
    {
        var owners = new Dictionary<(string Ns, string Inode), List<string>>();
        var namespaces = new Dictionary<string, string>();
        var result = new List<Connection>();
        try
        {
            string ns = Native.ReadLink("/proc/self/ns/net");
            namespaces[ns] = "/proc/self";
        }
        catch (Exception ex) when (Expected(ex)) { Error("network", ex); }
        foreach (var p in processes)
        {
            string root = "/proc/" + p.Pid;
            try
            {
                string ns = Native.ReadLink(root + "/ns/net");
                namespaces.TryAdd(ns, root);
                var pending = new List<(string Ns, string Inode)>();
                foreach (var fd in Directory.EnumerateFiles(root + "/fd").Take(4096))
                {
                    try
                    {
                        string? target = Native.ReadLink(fd);
                        if (target is not null && target.StartsWith("socket:[", StringComparison.Ordinal) && target.EndsWith(']')) pending.Add((ns, target[8..^1]));
                    }
                    catch (Exception ex) when (Expected(ex)) { Error("network", ex); }
                }
                if (ProcParsing.ParseProcessStat(ReadLimited(root + "/stat", 16384)).StartTicks != p.StartTicks || Native.ReadLink(root + "/ns/net") != ns) continue;
                foreach (var key in pending.Distinct())
                { if (!owners.TryGetValue(key, out var list)) owners[key] = list = []; list.Add(p.ProcessInstanceId); }
            }
            catch (Exception ex) when (Expected(ex)) { Error("network", ex); }
        }
        foreach (var (ns, root) in namespaces)
            foreach (string family in new[] { "tcp", "tcp6" })
            {
                try
                {
                    if (Native.ReadLink(root + "/ns/net") != ns) continue;
                    foreach (string line in File.ReadLines(root + "/net/" + family).Skip(1).Take(16384))
                    {
                        var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                        if (fields.Length < 10) continue;
                        var local = ProcParsing.ParseEndpoint(fields[1]);
                        var remote = ProcParsing.ParseEndpoint(fields[2]);
                        string state = fields[3] switch { "01" => "established", "0A" => "listen", "06" => "time_wait", _ => "other" };
                        if (owners.TryGetValue((ns, fields[9]), out var ids))
                            foreach (string id in ids.Distinct()) result.Add(new(ns, family == "tcp" ? "ipv4" : "ipv6", local.Address, local.Port, remote.Address, remote.Port, state, fields[9], id, "ok"));
                        else result.Add(new(ns, family == "tcp" ? "ipv4" : "ipv6", local.Address, local.Port, remote.Address, remote.Port, state, fields[9], null, "temporarily_unavailable"));
                    }
                }
                catch (Exception ex) when (Expected(ex)) { Error("network", ex); }
            }
        return result;
    }
}
