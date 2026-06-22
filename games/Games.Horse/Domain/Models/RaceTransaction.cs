namespace Games.Horse.Domain.Models;

public sealed record RaceTransaction(long UserId, long BalanceScopeId, int Amount);
