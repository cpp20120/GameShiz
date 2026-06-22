using Games.Poker.Domain;

namespace Games.Poker;

public sealed record LeaveResult(PokerError Error, TableSnapshot? Snapshot, bool TableClosed);
