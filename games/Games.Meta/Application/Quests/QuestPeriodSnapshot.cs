namespace Games.Meta.Application.Quests;

internal sealed record QuestPeriodSnapshot(
    long SeasonId,
    string PeriodKey,
    long Rows,
    long Started,
    long Completed,
    long Claimed);
