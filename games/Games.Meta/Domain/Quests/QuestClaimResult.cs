namespace Games.Meta.Domain.Quests;

public sealed record QuestClaimResult(
    string QuestId,
    string Title,
    long RewardXp,
    long RewardCoins,
    bool Claimed);
