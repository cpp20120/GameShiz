namespace BotFramework.Host.Contracts.Economics;

public sealed record WalletAccount(
    long UserId,
    long BalanceScopeId,
    string DisplayName,
    int Coins,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
