using System.Globalization;
using BotFramework.Host.Workflows;
using Dapper;

namespace Games.Meta.Application.Tournaments;

internal sealed class DirectTournamentWorkflow(TournamentCommandExecutor executor) : ITournamentWorkflow
{
    public Task<TournamentCreateResult> CreateAsync(long chatId, long userId, string gameKey, int entryFee, int maxPlayers, CancellationToken ct) =>
        executor.CreateAsync(chatId, userId, gameKey, entryFee, maxPlayers, NewCommandId("create"), ct);

    public Task<TournamentJoinResult> JoinAsync(long tournamentId, long userId, long chatId, string displayName, CancellationToken ct) =>
        executor.JoinAsync(tournamentId, userId, chatId, displayName, NewCommandId("join", tournamentId), ct);

    public Task<bool> StartAsync(long tournamentId, long userId, CancellationToken ct) =>
        executor.StartAsync(tournamentId, userId, NewCommandId("start", tournamentId), ct);

    public Task<TournamentReportResult> ReportMatchAsync(long matchId, long actorUserId, long victorUserId, CancellationToken ct) =>
        executor.ReportMatchAsync(matchId, actorUserId, victorUserId, NewCommandId("report", matchId), ct);

    public Task<TournamentPlayerInfo?> FinishAsync(long tournamentId, long actorUserId, long victorUserId, CancellationToken ct) =>
        executor.FinishAsync(tournamentId, actorUserId, victorUserId, NewCommandId("finish", tournamentId), ct);

    public Task<IReadOnlyList<TournamentPlayerInfo>?> CancelAsync(long tournamentId, long actorUserId, CancellationToken ct) =>
        executor.CancelAsync(tournamentId, actorUserId, NewCommandId("cancel", tournamentId), ct);

    private static string NewCommandId(string operation, long? aggregateId = null) =>
        aggregateId is null
            ? $"compat:tournament:{operation}:{Guid.NewGuid():N}"
            : $"compat:tournament:{operation}:{aggregateId.Value}:{Guid.NewGuid():N}";
}
