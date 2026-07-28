
namespace Games.Poker.Domain.Results;

public sealed record TableSnapshot(PokerTable Table, IReadOnlyList<PokerSeat> Seats);
