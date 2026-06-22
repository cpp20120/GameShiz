namespace Games.Meta.Domain.Seasons;

public sealed record SeasonRewardProcessResult(
    int Paid,
    IReadOnlyList<SeasonRewardPaidRow> Rows);
