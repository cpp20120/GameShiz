namespace Games.Leaderboard.Contracts;

public sealed record DailyClaimResponse(DailyClaimStatus Status, int BonusCoins, int NewBalance);
