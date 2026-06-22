namespace Games.Meta;

public sealed record SeasonRewardProcessResult(
    int Paid,
    IReadOnlyList<SeasonRewardPaidRow> Rows);
