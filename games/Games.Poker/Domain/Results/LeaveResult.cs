using Games.Poker.Domain;

namespace Games.Poker.Domain.Results;

public sealed record LeaveResult(PokerError Error, TableSnapshot? Snapshot, bool TableClosed);
