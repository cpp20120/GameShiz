namespace Games.Meta;

internal sealed record QuestSlot(
    string Id,
    string Period,
    IReadOnlyList<string> PoolTags,
    int Count,
    int RepeatCooldownPeriods);
