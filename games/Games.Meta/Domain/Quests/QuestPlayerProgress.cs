namespace Games.Meta.Domain.Quests;

public sealed record QuestPlayerProgress(
    int Level,
    int GamesPlayed,
    long TotalStaked);
