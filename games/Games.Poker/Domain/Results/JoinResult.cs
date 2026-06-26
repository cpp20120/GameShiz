
namespace Games.Poker.Domain.Results;

public sealed record JoinResult(PokerError Error, TableSnapshot? Snapshot, int Seated, int Max);
