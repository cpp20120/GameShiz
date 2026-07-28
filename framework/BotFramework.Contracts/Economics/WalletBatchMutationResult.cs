namespace BotFramework.Host.Contracts.Economics;

public readonly record struct WalletBatchMutationResult(
    bool Applied,
    bool Rejected,
    int NewBalance);
