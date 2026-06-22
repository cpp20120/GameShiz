namespace Games.Horse;

public sealed record RaceTransaction(long UserId, long BalanceScopeId, int Amount);
