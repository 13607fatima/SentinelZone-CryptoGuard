using System.Text.Json;
using System.Text.Json.Nodes;
using CryptoGuard.Compatibility.Windows.Contracts;

namespace CryptoGuard.Platform.Windows;

public static class ConfigurationLoader
{
    public static AgentConfig Load(string path)
    {
        if(new FileInfo(path).Length>1048576)throw new InvalidDataException("configuration_size");
        // Source-generated deserialization of init-only records can assign default(T)
        // to absent properties. Overlay explicitly supplied values on serialized defaults.
        var defaults=JsonSerializer.SerializeToNode(new AgentConfig(),WireJson.Default.AgentConfig)!.AsObject();
        var supplied=JsonNode.Parse(File.ReadAllBytes(path)) as JsonObject ?? throw new InvalidDataException("configuration_object");
        foreach(var property in supplied)defaults[property.Key]=property.Value?.DeepClone();
        var config=JsonSerializer.Deserialize(defaults.ToJsonString(),WireJson.Default.AgentConfig) ?? throw new InvalidDataException("configuration_object");
        config.Validate();
        return config;
    }
}
