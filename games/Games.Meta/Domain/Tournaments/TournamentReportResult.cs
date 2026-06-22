namespace Games.Meta;

public sealed record TournamentReportResult(
    bool Updated,
    bool Finished,
    string Message,
    TournamentMatchInfo? Match = null,
    TournamentPlayerInfo? Victor = null);
