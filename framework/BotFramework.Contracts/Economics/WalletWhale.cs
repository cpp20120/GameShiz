namespace BotFramework.Host.Contracts.Economics;

public sealed record WalletWhale(long UserId, long BalanceScopeId, int Coins, int Rank);
