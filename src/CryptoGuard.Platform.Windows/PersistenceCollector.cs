using System.Runtime.InteropServices;
using Microsoft.Win32;
using CryptoGuard.Compatibility.Windows.Contracts;
using CryptoGuard.Compatibility.Windows.Core;

namespace CryptoGuard.Platform.Windows;

public sealed class PersistenceCollector : IPersistenceCollector
{
    public PersistenceEntry[] Collect()
    {
        var output = new List<PersistenceEntry>();
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        foreach (var name in new[] { "Run", "RunOnce" })
        {
            var location = $"{hive}/{view}/Software/Microsoft/Windows/CurrentVersion/{name}";
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\" + name);
                if (key is null) continue;
                foreach (var value in key.GetValueNames()) output.Add(new(name, location + "/" + value, Privacy.ExecutableFromCommand(key.GetValue(value) as string), "observed"));
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException) { output.Add(new(name, location, null, "permission_denied")); }
        }
        foreach (var folder in new[] { Environment.SpecialFolder.Startup, Environment.SpecialFolder.CommonStartup })
        {
            var directory = Environment.GetFolderPath(folder);
            try
            {
                if (Directory.Exists(directory)) foreach (var file in Directory.EnumerateFiles(directory).Take(2000))
                    output.Add(new("startup", file, file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? file : ShortcutTarget(file), "observed"));
            }
            catch (UnauthorizedAccessException) { output.Add(new("startup", directory, null, "permission_denied")); }
        }
        try
        {
            using var services = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
            if (services is not null) foreach (var name in services.GetSubKeyNames())
            {
                using var service = services.OpenSubKey(name);
                if (service?.GetValue("Start") is int start && start <= 2)
                    output.Add(new("service", name, Privacy.ExecutableFromCommand(service.GetValue("ImagePath") as string), "observed"));
            }
        }
        catch (UnauthorizedAccessException) { output.Add(new("service", "SCM", null, "permission_denied")); }
        ReadTasks(output);
        return output.ToArray();
    }
    private static string? ShortcutTarget(string file)
    {
        if (!file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return null;
        object? shell = null, shortcut = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            shortcut = ((dynamic)shell!).CreateShortcut(file);
            return Privacy.ExecutableFromCommand("\"" + (string)((dynamic)shortcut).TargetPath + "\"");
        }
        catch (COMException) { return null; }
        finally { if (shortcut is not null) Marshal.FinalReleaseComObject(shortcut); if (shell is not null) Marshal.FinalReleaseComObject(shell); }
    }
    private static void ReadTasks(List<PersistenceEntry> output)
    {
        object? scheduler = null, root = null;
        try
        {
            scheduler = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!);
            ((dynamic)scheduler!).Connect(); root = ((dynamic)scheduler).GetFolder("\\"); Visit(root, output, 0);
        }
        catch (COMException) { output.Add(new("scheduled_task", "TaskScheduler", null, "permission_denied")); }
        finally { if (root is not null) Marshal.FinalReleaseComObject(root); if (scheduler is not null) Marshal.FinalReleaseComObject(scheduler); }
    }
    private static void Visit(dynamic folder, List<PersistenceEntry> output, int depth)
    {
        if (depth > 16 || output.Count >= 10000) return;
        object? tasks = null, folders = null;
        try
        {
            tasks = folder.GetTasks(1);
            foreach (dynamic task in (dynamic)tasks)
            {
                object? definition = null, actions = null;
                try
                {
                    definition = task.Definition; actions = ((dynamic)definition).Actions;
                    foreach (dynamic action in (dynamic)actions)
                    {
                        try { if ((int)action.Type == 0) output.Add(new("scheduled_task", (string)task.Path, Privacy.ExecutableFromCommand("\"" + (string)action.Path + "\""), "observed")); }
                        finally { Marshal.FinalReleaseComObject(action); }
                    }
                }
                finally { if (actions is not null) Marshal.FinalReleaseComObject(actions); if (definition is not null) Marshal.FinalReleaseComObject(definition); Marshal.FinalReleaseComObject(task); }
            }
            folders = folder.GetFolders(0);
            foreach (dynamic child in (dynamic)folders) { try { Visit(child, output, depth + 1); } finally { Marshal.FinalReleaseComObject(child); } }
        }
        finally { if (tasks is not null) Marshal.FinalReleaseComObject(tasks); if (folders is not null) Marshal.FinalReleaseComObject(folders); }
    }
}
