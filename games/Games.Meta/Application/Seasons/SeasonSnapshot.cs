namespace Games.Meta.Application.Seasons;

internal sealed record SeasonSnapshot(
    long SeasonId,
    string SeasonName,
    string Status,
    long Players,
    long Games,
    long Wins,
    long Losses,
    long Xp,
    long Stake,
    long Payout,
    decimal AvgLevel,
    long Clans);
