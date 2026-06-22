namespace Games.Meta.Domain.Quests;

public sealed record QuestProgressUpdate(
    string QuestId,
    int Progress,
    int Target,
    bool Completed);
