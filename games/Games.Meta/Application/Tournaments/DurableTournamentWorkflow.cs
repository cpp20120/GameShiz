using System.Globalization;
using BotFramework.Host.Workflows;
using Dapper;

namespace Games.Meta.Application.Tournaments;

public sealed class DurableTournamentWorkflow(
    IDurableWorkflowDispatcher dispatcher,
    INpgsqlConnectionFactory connections) : ITournamentWorkflow
{
    public Task<TournamentCreateResult> CreateAsync(long chatId, long userId, string gameKey, int entryFee, int maxPlayers, CancellationToken ct)
    {
        var token = Guid.NewGuid().ToString("N");
        var commandId = $"meta:tournament:create:{chatId}:{userId}:{token}";
        var workflowId = $"tournament:create:{token}";
        return DispatchAsync(
            new TournamentCreateWorkflowCommand(commandId, workflowId, chatId, userId, gameKey, entryFee, maxPlayers),
            new DurableWorkflowDispatchOptions(workflowId, commandId, "create"),
            () => new TournamentCreateResult(false, "Операция принята и выполняется.", Pending: true, CommandId: commandId),
            ct);
    }

    public Task<TournamentJoinResult> JoinAsync(long tournamentId, long userId, long chatId, string displayName, CancellationToken ct)
    {
        var commandId = $"meta:tournament:join:{tournamentId}:{chatId}:{userId}:{Guid.NewGuid():N}";
        var workflowId = TournamentWorkflowIds.ForTournament(tournamentId);
        return DispatchAsync(
            new TournamentJoinWorkflowCommand(commandId, workflowId, tournamentId, userId, chatId, displayName),
            new DurableWorkflowDispatchOptions(workflowId, commandId, "join", AggregateId: tournamentId.ToString(CultureInfo.InvariantCulture)),
            () => new TournamentJoinResult(false, "Операция принята и выполняется.", Pending: true, CommandId: commandId),
            ct);
    }

    public Task<bool> StartAsync(long tournamentId, long userId, CancellationToken ct)
    {
        var commandId = $"meta:tournament:start:{tournamentId}:{Guid.NewGuid():N}";
        var workflowId = TournamentWorkflowIds.ForTournament(tournamentId);
        return DispatchAsync(
            new TournamentStartWorkflowCommand(commandId, workflowId, tournamentId, userId),
            new DurableWorkflowDispatchOptions(workflowId, commandId, "start", AggregateId: tournamentId.ToString(CultureInfo.InvariantCulture)),
            static () => false,
            ct);
    }

    public async Task<TournamentReportResult> ReportMatchAsync(long matchId, long actorUserId, long victorUserId, CancellationToken ct)
    {
        var commandId = $"meta:tournament:report:{matchId}:{victorUserId}:{Guid.NewGuid():N}";
        var workflowId = await ResolveWorkflowIdAsync(matchId, ct);
        return await DispatchAsync(
            new TournamentReportWorkflowCommand(commandId, workflowId, matchId, actorUserId, victorUserId),
            new DurableWorkflowDispatchOptions(workflowId, commandId, "report", AggregateId: matchId.ToString(CultureInfo.InvariantCulture)),
            () => new TournamentReportResult(false, false, "Операция принята и выполняется.", Pending: true, CommandId: commandId),
            ct);
    }

    public Task<TournamentPlayerInfo?> FinishAsync(long tournamentId, long actorUserId, long victorUserId, CancellationToken ct)
    {
        var commandId = $"meta:tournament:finish:{tournamentId}:{victorUserId}:{Guid.NewGuid():N}";
        var workflowId = TournamentWorkflowIds.ForTournament(tournamentId);
        return DispatchAsync<TournamentPlayerInfo?>(
            new TournamentFinishWorkflowCommand(commandId, workflowId, tournamentId, actorUserId, victorUserId),
            new DurableWorkflowDispatchOptions(workflowId, commandId, "finish", AggregateId: tournamentId.ToString(CultureInfo.InvariantCulture)),
            static () => null,
            ct);
    }

    public Task<IReadOnlyList<TournamentPlayerInfo>?> CancelAsync(long tournamentId, long actorUserId, CancellationToken ct)
    {
        var commandId = $"meta:tournament:cancel:{tournamentId}:{Guid.NewGuid():N}";
        var workflowId = TournamentWorkflowIds.ForTournament(tournamentId);
        return DispatchAsync<IReadOnlyList<TournamentPlayerInfo>?>(
            new TournamentCancelWorkflowCommand(commandId, workflowId, tournamentId, actorUserId),
            new DurableWorkflowDispatchOptions(workflowId, commandId, "cancel", AggregateId: tournamentId.ToString(CultureInfo.InvariantCulture)),
            static () => null,
            ct);
    }

    private Task<TResult> DispatchAsync<TResult>(
        object command,
        DurableWorkflowDispatchOptions options,
        Func<TResult> pending,
        CancellationToken ct) =>
        dispatcher.DispatchAsync(command, options, pending, ct);

    private async Task<string> ResolveWorkflowIdAsync(long matchId, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        var tournamentId = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT tournament_id FROM meta_tournament_matches WHERE id = @matchId",
            new { matchId },
            cancellationToken: ct));
        return TournamentWorkflowIds.ForTournament(tournamentId ?? matchId);
    }
}
