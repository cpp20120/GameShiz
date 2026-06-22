namespace Games.Meta;

internal sealed record QuestCondition(
    string Id,
    long? MinStake = null,
    long? MaxStake = null,
    long? MinPayout = null,
    long? MinProfit = null,
    decimal? MinMultiplier = null);
