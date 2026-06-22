namespace Games.Meta;

internal sealed record QuestProgressRow(
    string QuestId,
    string PeriodKey,
    int Progress,
    int Target,
    bool Completed,
    bool Claimed);
