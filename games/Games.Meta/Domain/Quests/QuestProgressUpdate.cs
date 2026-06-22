namespace Games.Meta;

public sealed record QuestProgressUpdate(
    string QuestId,
    int Progress,
    int Target,
    bool Completed);
