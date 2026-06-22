using Games.Poker.Domain;

namespace Games.Poker.Domain.Results;

public sealed record CreateResult(PokerError Error, string InviteCode, int BuyIn);
