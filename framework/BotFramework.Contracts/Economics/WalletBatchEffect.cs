namespace BotFramework.Host.Contracts.Economics;

public sealed record WalletBatchEffect(
    WalletBatchEffectKind Kind,
    int Amount,
    string Reason);
