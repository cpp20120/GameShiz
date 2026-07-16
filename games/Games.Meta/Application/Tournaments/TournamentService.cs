using BotFramework.Host.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Meta.Application.Tournaments;

public sealed class TournamentService : ITournamentService
{
    private readonly IMetaService _meta;
    private readonly ITournamentStore _tournaments;
    private readonly ITournamentWorkflow _workflow;

    [ActivatorUtilitiesConstructor]
    public TournamentService(IMetaService meta, ITournamentStore tournaments, ITournamentWorkflow workflow)
    {
        _meta = meta;
        _tournaments = tournaments;
        _workflow = workflow;
    }

    // Keeps the existing unit-test and module-level construction seam intact.
    // Production composition selects the durable-workflow constructor above.
    public TournamentService(IMetaService meta, ITournamentStore tournaments, IAtomicEffectExecutor effects)
        : this(meta, tournaments, new DirectTournamentWorkflow(new TournamentCommandExecutor(meta, tournaments, effects)))
    {
    }

    public Task<TournamentCreateResult> CreateAsync(
        long chatId,
        long userId,
        string gameKey,
        int entryFee,
        int maxPlayers,
        CancellationToken ct) =>
        _workflow.CreateAsync(chatId, userId, gameKey, entryFee, maxPlayers, ct);

    public Task<TournamentJoinResult> JoinAsync(
        long tournamentId,
        long userId,
        long chatId,
        string displayName,
        CancellationToken ct) =>
        _workflow.JoinAsync(tournamentId, userId, chatId, displayName, ct);

    public Task<TournamentInfo?> GetAsync(long tournamentId, CancellationToken ct) =>
        _tournaments.GetAsync(tournamentId, ct);

    public async Task<IReadOnlyList<TournamentInfo>> GetOpenAsync(long chatId, int limit, CancellationToken ct)
    {
        var season = await _meta.GetActiveSeasonAsync(ct).ConfigureAwait(false);
        return await _tournaments.GetOpenAsync(season, chatId, limit, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<TournamentPlayerInfo>> GetPlayersAsync(long tournamentId, CancellationToken ct) =>
        _tournaments.GetPlayersAsync(tournamentId, ct);

    public Task<IReadOnlyList<TournamentMatchInfo>> GetMatchesAsync(long tournamentId, CancellationToken ct) =>
        _tournaments.GetMatchesAsync(tournamentId, ct);

    public Task<bool> StartAsync(long tournamentId, long userId, CancellationToken ct) =>
        _workflow.StartAsync(tournamentId, userId, ct);

    public Task<TournamentReportResult> ReportMatchAsync(long matchId, long actorUserId, long victorUserId, CancellationToken ct) =>
        _workflow.ReportMatchAsync(matchId, actorUserId, victorUserId, ct);

    public Task<TournamentPlayerInfo?> FinishAsync(long tournamentId, long actorUserId, long victorUserId, CancellationToken ct) =>
        _workflow.FinishAsync(tournamentId, actorUserId, victorUserId, ct);

    public Task<IReadOnlyList<TournamentPlayerInfo>?> CancelAsync(long tournamentId, long actorUserId, CancellationToken ct) =>
        _workflow.CancelAsync(tournamentId, actorUserId, ct);
}
