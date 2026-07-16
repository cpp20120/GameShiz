namespace Games.Meta.Domain.Tournaments;

public sealed record TournamentReportResult(
    bool Updated,
    bool Finished,
    string Message,
    TournamentMatchInfo? Match = null,
    TournamentPlayerInfo? Victor = null,
    bool Pending = false,
    string? CommandId = null);
