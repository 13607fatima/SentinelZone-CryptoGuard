using CryptoGuard.Contracts;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace CryptoGuard.Platform.Linux;

public sealed partial class LinuxCollector
{
    public (List<Device> Devices, List<Sensor> Sensors) CollectHardware()
    {
        var foundDevices = new List<Device>();
        var foundSensors = new List<Sensor>();
        void ReadSensor(string file, string id, string name, string type, string source, double divisor, string unit)
        {
            Metric reading;
            try { reading = Metric.Ok(double.Parse(File.ReadAllText(file).Trim(), CultureInfo.InvariantCulture) / divisor, unit, source); }
            catch (Exception ex) when (Expected(ex)) { reading = Metric.Missing(unit, source, Status(ex)); Error("hardware", ex); }
            foundSensors.Add(new(source + ":" + id + ":" + Path.GetFileName(file), id, name, type, reading));
        }
        try
        {
            foreach (string root in Directory.EnumerateDirectories("/sys/class/hwmon", "hwmon*").Take(128))
            {
                string name = File.Exists(root + "/name") ? File.ReadAllText(root + "/name").Trim() : "unknown";
                string id = "hwmon:" + (new DirectoryInfo(root).ResolveLinkTarget(true)?.FullName ?? root);
                foundDevices.Add(new(id, "unknown", name, "hwmon", "device"));
                foreach (string input in Directory.EnumerateFiles(root, "temp*_input").Take(128))
                {
                    string labelFile = input.Replace("_input", "_label", StringComparison.Ordinal);
                    string label = File.Exists(labelFile) ? File.ReadAllText(labelFile).Trim() : Path.GetFileName(input);
                    ReadSensor(input, id, name + ":" + label, "temperature", "hwmon", 1000, "celsius");
                }
            }
            foreach (string root in Directory.EnumerateDirectories("/sys/class/thermal", "thermal_zone*").Take(128))
            {
                string type = File.Exists(root + "/type") ? File.ReadAllText(root + "/type").Trim() : "unknown";
                string id = "thermal:" + Path.GetFileName(root);
                foundDevices.Add(new(id, "unknown", type, "thermal", "device"));
                ReadSensor(root + "/temp", id, type, "temperature", "thermal", 1000, "celsius");
            }
            foreach (string root in Directory.EnumerateDirectories("/sys/class/drm", "card*").Where(p => int.TryParse(Path.GetFileName(p)[4..], out _)).Take(64))
            {
                string vendor = File.Exists(root + "/device/vendor") ? File.ReadAllText(root + "/device/vendor").Trim() : "unknown";
                string name = vendor switch { "0x1002" => "AMD", "0x8086" => "Intel", "0x10de" => "NVIDIA", "0x15ad" => "VMware virtual GPU", _ => "DRM device" };
                string id = "drm:" + (new DirectoryInfo(root + "/device").ResolveLinkTarget(true)?.FullName ?? root);
                foundDevices.Add(new(id, vendor, name, "drm.sysfs", "device"));
                if (File.Exists(root + "/device/gpu_busy_percent")) ReadSensor(root + "/device/gpu_busy_percent", id, name + " busy", "utilization", "drm.sysfs", 1, "percent");
                else foundSensors.Add(new(id + ":utilization", id, name + " busy", "utilization", Metric.Missing("percent", "drm.sysfs")));
            }
        }
        catch (Exception ex) when (Expected(ex)) { Error("hardware", ex); }
        try { Nvml.Read(foundDevices, foundSensors); }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException) { errors.Add(new("nvml", "unsupported", "driver_library_unavailable")); }
        return (foundDevices, foundSensors);
    }
    public Metric CollectIdle()
    {
        try
        {
            string? list = ExecutableInspector.RunReadOnly("/usr/bin/loginctl", ["list-sessions", "--no-legend", "--no-pager"]);
            if (list is null) return Metric.Missing("seconds", "logind", "temporarily_unavailable");
            var durations = new List<double>();
            foreach (string line in list.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(64))
            {
                string session = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
                if (!session.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')) continue;
                string? detail = ExecutableInspector.RunReadOnly("/usr/bin/loginctl", ["show-session", session, "--no-pager", "-p", "Type", "-p", "Remote", "-p", "Active", "-p", "IdleHint", "-p", "IdleSinceHintMonotonic"]);
                if (detail is null) return Metric.Missing("seconds", "logind", "temporarily_unavailable");
                var fields = detail.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0], p => p[1]);
                if (fields.GetValueOrDefault("Type") is not ("x11" or "wayland") || fields.GetValueOrDefault("Remote") != "no" || fields.GetValueOrDefault("Active") != "yes") continue;
                if (fields.GetValueOrDefault("IdleHint") == "no") durations.Add(0);
                else if (double.TryParse(fields.GetValueOrDefault("IdleSinceHintMonotonic"), CultureInfo.InvariantCulture, out double micros) && micros > 0)
                    durations.Add(Math.Max(0, Environment.TickCount64 / 1000.0 - micros / 1_000_000.0));
                else return Metric.Missing("seconds", "logind", "temporarily_unavailable");
            }
            return durations.Count == 0 ? Metric.Missing("seconds", "logind") : Metric.Ok(durations.Min(), "seconds", "logind_idle_hint");
        }
        catch (Exception ex) when (Expected(ex)) { Error("idle", ex); return Metric.Missing("seconds", "logind", Status(ex)); }
    }
}

internal static unsafe partial class Nvml
{
    private const string Library = "libnvidia-ml.so.1";
    [StructLayout(LayoutKind.Sequential)] private struct Utilization { public uint Gpu, Memory; }
    [LibraryImport(Library, EntryPoint = "nvmlInit_v2")] private static partial int Init();
    [LibraryImport(Library, EntryPoint = "nvmlShutdown")] private static partial int Shutdown();
    [LibraryImport(Library, EntryPoint = "nvmlDeviceGetCount_v2")] private static partial int Count(out uint count);
    [LibraryImport(Library, EntryPoint = "nvmlDeviceGetHandleByIndex_v2")] private static partial int Handle(uint index, out nint handle);
    [LibraryImport(Library, EntryPoint = "nvmlDeviceGetName")] private static partial int Name(nint handle, byte* name, uint length);
    [LibraryImport(Library, EntryPoint = "nvmlDeviceGetUUID")] private static partial int Uuid(nint handle, byte* uuid, uint length);
    [LibraryImport(Library, EntryPoint = "nvmlDeviceGetUtilizationRates")] private static partial int Util(nint handle, out Utilization value);
    [LibraryImport(Library, EntryPoint = "nvmlDeviceGetTemperature")] private static partial int Temperature(nint handle, uint kind, out uint value);
    private static string Status(int result) => result == 0 ? "ok" : result == 4 ? "permission_denied" : result is 3 or 9 ? "unsupported" : "temporarily_unavailable";
    public static void Read(List<Device> devices, List<Sensor> sensors)
    {
        int init = Init();
        if (init != 0) { sensors.Add(new("nvml:availability", "nvml", "NVIDIA availability", "availability", Metric.Missing("status", "nvml", Status(init)))); return; }
        try
        {
            if (Count(out uint count) != 0) return;
            byte* buffer = stackalloc byte[128];
            for (uint index = 0; index < Math.Min(count, 64); index++)
            {
                if (Handle(index, out nint device) != 0) continue;
                string name = "NVIDIA GPU";
                if (Name(device, buffer, 128) == 0) name = Marshal.PtrToStringUTF8((nint)buffer) ?? name;
                string id = "nvml:" + index;
                if (Uuid(device, buffer, 128) == 0) id = "nvml:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Marshal.PtrToStringUTF8((nint)buffer) ?? id))).ToLowerInvariant();
                devices.Add(new(id, "NVIDIA", name, "nvml", "device"));
                int u = Util(device, out var utilization), t = Temperature(device, 0, out uint temperature);
                sensors.Add(new(id + ":gpu", id, "GPU utilization", "utilization", u == 0 ? Metric.Ok(utilization.Gpu, "percent", "nvml") : Metric.Missing("percent", "nvml", Status(u))));
                sensors.Add(new(id + ":temperature", id, "GPU temperature", "temperature", t == 0 ? Metric.Ok(temperature, "celsius", "nvml") : Metric.Missing("celsius", "nvml", Status(t))));
            }
        }
        finally { Shutdown(); }
    }
}
