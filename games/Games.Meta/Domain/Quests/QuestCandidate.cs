namespace Games.Meta;

internal sealed record QuestCandidate(
    QuestTemplate Template,
    IReadOnlyList<string> Tags,
    string RepeatKey,
    int Weight);
