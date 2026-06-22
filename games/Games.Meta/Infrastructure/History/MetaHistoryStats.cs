namespace Games.Meta.Infrastructure.History;

public sealed record MetaHistoryStats(
    long TotalEvents,
    long GameCompletedEvents,
    long TournamentEvents,
    DateTimeOffset? FirstEventAt,
    DateTimeOffset? LastEventAt);
