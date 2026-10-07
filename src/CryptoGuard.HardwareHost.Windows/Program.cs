using System.Text.Json;
using CryptoGuard.Compatibility.Windows.Contracts;
using LibreHardwareMonitor.Hardware;

var readings = new List<Sensor>();
var computer = new Computer { IsCpuEnabled = args.Contains("--cpu"), IsGpuEnabled = true,
    IsMemoryEnabled = true, IsMotherboardEnabled = false, IsControllerEnabled = false, IsStorageEnabled = false, IsNetworkEnabled = false };
try
{
    computer.Open();
    foreach(var device in computer.Hardware) Warm(device);
    await Task.Delay(500);
    do
    {
        if(args.Contains("--serve"))
        {
            var command=Console.ReadLine();
            if(command is null or "quit")break;
            if(command!="sample")return 3;
        }
        readings.Clear();
        foreach (var hardware in computer.Hardware) Visit(hardware);
        Console.WriteLine(JsonSerializer.Serialize(readings.ToArray(), WireJson.Default.SensorArray));
        Console.Out.Flush();
    }while(args.Contains("--serve"));
    return 0;
    void Warm(IHardware hardware) { hardware.Update();foreach(var child in hardware.SubHardware)Warm(child); }
    void Visit(IHardware hardware)
    {
        hardware.Update();
        foreach (var sensor in hardware.Sensors)
        {
            var unit = sensor.SensorType switch { SensorType.Temperature => "celsius", SensorType.Load => "percent", SensorType.Power => "watts", SensorType.Clock => "megahertz", SensorType.Fan => "rpm", _ => null };
            if (unit is null) continue;
            var value = sensor.Value;
            readings.Add(new(hardware.Identifier.ToString(), hardware.Name, sensor.Identifier.ToString(), sensor.Name, sensor.SensorType.ToString(), unit,
                value is float v && float.IsFinite(v) ? v : null, value is float finite && float.IsFinite(finite) ? "ok" : "temporarily_unavailable", "LibreHardwareMonitorLib/0.9.6.device_level"));
        }
        foreach (var child in hardware.SubHardware) Visit(child);
    }
}
catch (Exception ex) when (ex is not OutOfMemoryException)
{
    Console.Error.WriteLine("hardware_unavailable:" + ex.GetType().Name); return 2;
}
finally { computer.Close(); }
