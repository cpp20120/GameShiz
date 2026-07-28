namespace BotFramework.Contracts.Operations;

public sealed record EconomySimulationReport(long Emission, long Sinks, double Rtp,
    IReadOnlyList<int> FinalBalances, IReadOnlyList<string> Warnings);
