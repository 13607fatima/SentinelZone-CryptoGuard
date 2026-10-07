using System.Text.Json;
using CryptoGuard.Contracts;

namespace CryptoGuard.Risk;

public static class ReplayRunner
{
    public static void Run(string input,AgentConfig config,TextWriter output)
    {
        if(new FileInfo(input).Length>256L*1024*1024)throw new InvalidDataException("replay_file_limit");
        var engine=new RuleEngine(config);
        double elapsed=0;
        foreach(var line in File.ReadLines(input))
        {
            if(line.Length>16*1024*1024)throw new InvalidDataException("replay_event_limit");
            var item=JsonSerializer.Deserialize(line,ContractJson.Default.TelemetryEvent) ?? throw new InvalidDataException("replay_json");
            CryptoGuard.Core.LocalCommands.Validate(item);
            if(item.Snapshot is not Snapshot snapshot)continue;
            elapsed+=item.SampleDurationMs;
            snapshot.ObservedAt=item.ObservedAt;
            snapshot.MonotonicMs=elapsed;
            output.WriteLine(JsonSerializer.Serialize(engine.Evaluate(snapshot),ContractJson.Default.ListRiskResult));
        }
    }
}
