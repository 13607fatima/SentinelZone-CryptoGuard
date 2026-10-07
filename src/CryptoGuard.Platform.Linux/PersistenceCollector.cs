using CryptoGuard.Contracts;
using CryptoGuard.Core;

namespace CryptoGuard.Platform.Linux;

public sealed partial class LinuxCollector
{
    public List<PersistenceEntry> CollectPersistence(IReadOnlyList<ProcessSample> processes)
    {
        var result = new List<PersistenceEntry>();
        var enabledUnits = new HashSet<string>(StringComparer.Ordinal);
        var roots = new List<(string Path, string Kind)>
        {
            ("/etc/systemd/system", "systemd"), ("/usr/lib/systemd/system", "systemd"),
            ("/etc/cron.d", "cron"), ("/var/spool/cron/crontabs", "cron"), ("/etc/xdg/autostart", "xdg")
            ,("/etc/cron.hourly","cron_directory"),("/etc/cron.daily","cron_directory"),("/etc/cron.weekly","cron_directory"),("/etc/cron.monthly","cron_directory")
        };
        try
        {
            var homes = File.ReadLines("/etc/passwd").Select(line => line.Split(':')).Where(f => f.Length >= 7 && int.TryParse(f[2], out int uid) && (uid == 0 || uid >= 1000) && f[5].StartsWith('/')).Select(f => f[5]).Distinct().Take(128);
            foreach (string home in homes)
            { roots.Add((home + "/.config/systemd/user", "systemd")); roots.Add((home + "/.config/autostart", "xdg")); }
        }
        catch (Exception ex) when (Expected(ex)) { Error("persistence", ex); }
        var paths = new List<(string Path, string Kind)> { ("/etc/crontab", "cron") };
        foreach (var (root, kind) in roots)
        {
            try
            {
                // No recursive traversal through unit symlinks or arbitrary user trees.
                paths.AddRange(Directory.EnumerateFiles(root).Take(4096).Select(p => (p, kind)));
                if (kind == "systemd")
                {
                    foreach (var links in Directory.EnumerateDirectories(root).Where(p => p.EndsWith(".wants", StringComparison.Ordinal) || p.EndsWith(".requires", StringComparison.Ordinal)).Take(256))
                        foreach (var unit in Directory.EnumerateFiles(links).Take(4096))
                        {
                            enabledUnits.Add(Path.GetFileName(unit));
                            if (unit.EndsWith(".timer", StringComparison.Ordinal)) enabledUnits.Add(Path.GetFileNameWithoutExtension(unit) + ".service");
                        }
                    foreach (var d in Directory.EnumerateDirectories(root, "*.d").Take(256)) paths.AddRange(Directory.EnumerateFiles(d, "*.conf").Take(32).Select(p => (p, kind)));
                }
            }
            catch (DirectoryNotFoundException) { }
            catch (Exception ex) when (Expected(ex)) { Error("persistence", ex); }
        }
        foreach (var (path, kind) in paths.Take(8192))
        {
            try
            {
                if (kind == "systemd" && !path.EndsWith(".service", StringComparison.Ordinal) && !path.EndsWith(".timer", StringComparison.Ordinal) && !path.EndsWith(".conf", StringComparison.Ordinal)) continue;
                if (kind == "xdg" && !path.EndsWith(".desktop", StringComparison.Ordinal)) continue;
                var info = new FileInfo(path);
                if (info.LinkTarget is not null || !info.Exists || info.Length > 262144) continue;
                if(kind=="cron_directory")
                {
                    var owner=processes.FirstOrDefault(p=>p.ExePath==path);
                    bool executable=(File.GetUnixFileMode(path) & (UnixFileMode.UserExecute|UnixFileMode.GroupExecute|UnixFileMode.OtherExecute))!=0;
                    result.Add(new(kind,Privacy.PathLabel(path),path,owner?.ProcessInstanceId,executable?"ok":"disabled"));
                    continue;
                }
                string[] lines = File.ReadAllLines(path);
                bool disabled = kind == "xdg" && lines.Any(s => s.Trim().Equals("Hidden=true", StringComparison.OrdinalIgnoreCase) || s.Trim().Equals("X-GNOME-Autostart-enabled=false", StringComparison.OrdinalIgnoreCase));
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
                    string? command = kind switch
                    {
                        "systemd" when trimmed.StartsWith("ExecStart=", StringComparison.Ordinal) => trimmed[10..].TrimStart('-', '+', '!', ':', '@'),
                        "xdg" when trimmed.StartsWith("Exec=", StringComparison.Ordinal) => trimmed[5..],
                        "cron" => CronCommand(trimmed, path.Contains("/crontabs/", StringComparison.Ordinal)),
                        _ => null
                    };
                    if (command is null) continue;
                    string? executable = FirstExecutable(command);
                    // Arguments and environment settings never leave this method.
                    var process = processes.FirstOrDefault(p => p.ExePath == executable);
                    string status = disabled ? "disabled" : executable is null || kind == "systemd" && !enabledUnits.Contains(Path.GetFileName(path)) ? "partial" : "ok";
                    result.Add(new(kind, Privacy.PathLabel(path), executable, process?.ProcessInstanceId, status));
                }
                if (path.EndsWith(".timer", StringComparison.Ordinal)) result.Add(new("systemd_timer", Privacy.PathLabel(path), null, null, "partial"));
            }
            catch (Exception ex) when (Expected(ex)) { Error("persistence", ex); }
        }
        return result.Distinct().Take(8192).ToList();
    }
    public static string? FirstExecutable(string command)
    {
        command = command.TrimStart();
        if (command.Length == 0) return null;
        string first;
        if (command[0] is '\'' or '"') { int end = command.IndexOf(command[0], 1); if (end < 0) return null; first = command[1..end]; }
        else first = command.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
        return first.StartsWith('/') && first.IndexOfAny(['$', '`', ';', '|']) < 0 ? first : null;
    }
    public static string? CronCommand(string line, bool perUser)
    {
        line = line.Trim();
        if (line.Length == 0 || line.StartsWith('#')) return null;
        int equals = line.IndexOf('=');
        if (equals >= 0)
        {
            string name = line[..equals].TrimEnd();
            if (name.Length > 0 && (char.IsAsciiLetter(name[0]) || name[0] == '_') &&
                name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')) return null;
        }
        int skip = line.StartsWith('@') ? (perUser ? 1 : 2) : (perUser ? 5 : 6);
        // Limit splitting to the schedule/user fields so quoted command paths
        // and argument whitespace are preserved, including key=value options.
        var fields = line.Split((char[]?)null, skip + 1, StringSplitOptions.RemoveEmptyEntries);
        return fields.Length > skip ? fields[skip] : null;
    }
}
