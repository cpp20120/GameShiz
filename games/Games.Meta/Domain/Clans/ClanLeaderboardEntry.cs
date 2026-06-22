namespace Games.Meta;

public sealed record ClanLeaderboardEntry(
    int Place,
    long ClanId,
    string Name,
    string Tag,
    int Members,
    long Xp,
    int Rating);
