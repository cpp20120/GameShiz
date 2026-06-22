using Games.Poker.Domain;

namespace Games.Poker;

public sealed record JoinResult(PokerError Error, TableSnapshot? Snapshot, int Seated, int Max);
