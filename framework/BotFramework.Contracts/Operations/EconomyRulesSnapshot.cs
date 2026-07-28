namespace BotFramework.Contracts.Operations;

public sealed record EconomyRulesSnapshot(int StartingBalance, int Stake, int WinPayout,
    double WinProbability, int InflationWarningThreshold);
