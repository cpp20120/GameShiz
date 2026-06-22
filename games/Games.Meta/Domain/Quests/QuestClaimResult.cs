namespace Games.Meta;

public sealed record QuestClaimResult(
    string QuestId,
    string Title,
    long RewardXp,
    long RewardCoins,
    bool Claimed);
