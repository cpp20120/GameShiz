namespace Games.Meta;

public interface ITournamentStore
{
    Task<TournamentCreateResult> CreateAsync(MetaSeason season, long chatId, long createdBy, string gameKey, int entryFee, int maxPlayers, CancellationToken ct);
    Task<TournamentJoinResult> JoinAsync(long tournamentId, long userId, string displayName, CancellationToken ct);
    Task<TournamentInfo?> GetAsync(long tournamentId, CancellationToken ct);
    Task<IReadOnlyList<TournamentInfo>> GetOpenAsync(MetaSeason season, long chatId, int limit, CancellationToken ct);
    Task<IReadOnlyList<TournamentPlayerInfo>> GetPlayersAsync(long tournamentId, CancellationToken ct);
    Task<IReadOnlyList<TournamentMatchInfo>> GetMatchesAsync(long tournamentId, CancellationToken ct);
    Task<bool> StartAsync(long tournamentId, long userId, CancellationToken ct);
    Task<TournamentReportResult> ReportMatchAsync(long matchId, long actorUserId, long victorUserId, CancellationToken ct);
    Task<TournamentPlayerInfo?> FinishAsync(long tournamentId, long actorUserId, long winnerUserId, CancellationToken ct);
    Task<IReadOnlyList<TournamentPlayerInfo>?> CancelAsync(long tournamentId, long actorUserId, CancellationToken ct);
}
