namespace Games.Meta;

public sealed record PlayerQuestView(
    string Id,
    string Title,
    string Description,
    string Period,
    int Progress,
    int Target,
    bool Completed,
    bool Claimed,
    long RewardXp,
    long RewardCoins);
