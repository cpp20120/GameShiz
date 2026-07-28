namespace BotFramework.Contracts.Operations;

public sealed record EconomySimulationRequest(EconomyRulesSnapshot Rules, int Players, int Rounds, int Seed);
