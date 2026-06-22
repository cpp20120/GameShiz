namespace Games.Meta.Domain.Quests;

internal sealed record QuestCandidate(
    QuestTemplate Template,
    IReadOnlyList<string> Tags,
    string RepeatKey,
    int Weight);
