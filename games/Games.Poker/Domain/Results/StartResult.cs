using Games.Poker.Domain;

namespace Games.Poker.Domain.Results;

public sealed record StartResult(PokerError Error, TableSnapshot? Snapshot);
