namespace Games.Meta;

public sealed record SeasonRewardPaidRow(
    int Place,
    long ChatId,
    long UserId,
    string DisplayName,
    int Amount);
