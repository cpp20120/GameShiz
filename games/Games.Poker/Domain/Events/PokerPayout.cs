namespace Games.Poker.Domain.Events;

public sealed record PokerPayout(long UserId, int Amount);
