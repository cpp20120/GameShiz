
namespace Games.Poker.Domain.Results;

public sealed record TableSnapshot(PokerTable Table, List<PokerSeat> Seats);
