namespace Games.Poker.Domain;

public sealed record ShowdownEntry(PokerSeat Seat, HandRank? Rank, int Won, string HoleCards);
