using CryptoGuard.Contracts;
using CryptoGuard.Core;
using CryptoGuard.Risk;
using System.Text.Json;

if (args.Length < 1) { Console.Error.WriteLine("Usage: cryptoguard-replay INPUT.jsonl [agent.json]"); return 2; }
var config = args.Length > 1 ? JsonSerializer.Deserialize(File.ReadAllText(args[1]), ContractJson.Default.AgentConfig) ?? throw new InvalidDataException("config") : new AgentConfig();
// Replay consumes policy and normalized samples; source-OS storage paths are not used.
Configuration.Validate(config, validatePaths: false);
ReplayRunner.Run(args[0],config,Console.Out);
return 0;
