namespace Games.Meta;

public sealed record MetaReconstructionSummary(
    long GameCompletedEvents,
    long AchievementEvents,
    long ReconstructablePlayers,
    long ReconstructableAchievements,
    long CurrentPlayers,
    long CurrentAchievements);
