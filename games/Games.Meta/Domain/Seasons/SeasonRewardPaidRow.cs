namespace Games.Meta.Domain.Seasons;

public sealed record SeasonRewardPaidRow(
    int Place,
    long ChatId,
    long UserId,
    string DisplayName,
    int Amount);
