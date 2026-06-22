namespace Games.Meta;

internal sealed record ClanSeasonRewardWinner(
    int Place,
    long ChatId,
    long ClanId,
    string ClanName,
    string ClanTag,
    long OwnerUserId,
    string OwnerDisplayName,
    long Xp,
    int Rating);
