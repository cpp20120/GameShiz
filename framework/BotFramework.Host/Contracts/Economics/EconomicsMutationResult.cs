namespace BotFramework.Host;

public readonly record struct EconomicsMutationResult(
    bool Applied,
    bool Rejected,
    int NewBalance);
