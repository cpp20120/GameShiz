namespace BotFramework.Sdk.Economics;

public sealed record WalletMutationDecision(
    bool Applied,
    bool Rejected,
    int NewBalance,
    long NewVersion,
    IReadOnlyList<WalletMutationLine> Ledger);
