namespace Games.Poker.Domain.Results;

public sealed record ShowdownEntry(PokerSeat Seat, HandRank? Rank, int Won, string HoleCards);
