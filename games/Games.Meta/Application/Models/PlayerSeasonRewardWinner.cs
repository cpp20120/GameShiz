namespace Games.Meta;

internal sealed record PlayerSeasonRewardWinner(
    int Place,
    long ChatId,
    long UserId,
    string DisplayName,
    long Xp,
    int Rating);
