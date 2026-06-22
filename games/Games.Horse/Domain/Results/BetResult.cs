namespace Games.Horse;

public sealed record BetResult(HorseError Error, int HorseId, int Amount, int RemainingCoins);
