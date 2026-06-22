namespace Games.Meta.Infrastructure.Persistence;

internal sealed record QuestProgressRow(
    string QuestId,
    string PeriodKey,
    int Progress,
    int Target,
    bool Completed,
    bool Claimed);
