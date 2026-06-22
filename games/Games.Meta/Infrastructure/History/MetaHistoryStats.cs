namespace Games.Meta;

public sealed record MetaHistoryStats(
    long TotalEvents,
    long GameCompletedEvents,
    long TournamentEvents,
    DateTimeOffset? FirstEventAt,
    DateTimeOffset? LastEventAt);
