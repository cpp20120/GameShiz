namespace Games.Meta;

public sealed record QuestTemplate(
    string Id,
    string Title,
    string Description,
    string Period,
    string Kind,
    string? GameKey,
    int Target,
    long RewardXp,
    long RewardCoins,
    long? MinStake = null,
    long? MaxStake = null,
    long? MinPayout = null,
    long? MinProfit = null,
    decimal? MinMultiplier = null,
    string Rarity = "common",
    string Cluster = "core",
    int MinLevel = 0,
    int MinGamesPlayed = 0,
    long MinTotalStaked = 0);

public sealed record QuestPlayerProgress(
    int Level,
    int GamesPlayed,
    long TotalStaked);

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

public sealed record QuestProgressUpdate(
    string QuestId,
    int Progress,
    int Target,
    bool Completed);

public sealed record QuestClaimResult(
    string QuestId,
    string Title,
    long RewardXp,
    long RewardCoins,
    bool Claimed);
