using Games.Poker.Domain;

namespace Games.Poker;

public sealed record StartResult(PokerError Error, TableSnapshot? Snapshot);
