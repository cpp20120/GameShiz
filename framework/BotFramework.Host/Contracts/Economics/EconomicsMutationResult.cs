namespace BotFramework.Host.Contracts.Economics;

public readonly record struct EconomicsMutationResult(
    bool Applied,
    bool Rejected,
    int NewBalance);
