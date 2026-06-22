namespace Games.Meta.Application.Analytics;

internal sealed record PlayerRtpSnapshot(
    long SeasonId,
    long ChatId,
    long UserId,
    string DisplayName,
    long Stake,
    long Payout,
    decimal Rtp);
