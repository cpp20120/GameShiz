namespace Games.Meta.Domain.Quests;

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
