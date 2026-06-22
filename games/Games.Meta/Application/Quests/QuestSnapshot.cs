namespace Games.Meta.Application.Quests;

internal sealed record QuestSnapshot(
    string QuestId,
    long Rows,
    long Started,
    long Completed,
    long Claimed,
    decimal AvgProgressRatio);
