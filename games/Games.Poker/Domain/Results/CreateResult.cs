using Games.Poker.Domain;

namespace Games.Poker;

public sealed record CreateResult(PokerError Error, string InviteCode, int BuyIn);
