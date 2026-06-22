using Games.Poker.Domain;

namespace Games.Poker;

public sealed record TableSnapshot(PokerTable Table, List<PokerSeat> Seats);
