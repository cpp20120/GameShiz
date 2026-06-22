namespace Games.Meta.Application.Analytics;

internal sealed record WhaleSnapshot(long UserId, long BalanceScopeId, int Coins, int Rank);
