namespace Games.Horse.Domain.Results;

public sealed record BetResult(HorseError Error, int HorseId, int Amount, int RemainingCoins);
