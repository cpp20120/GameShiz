using System.Globalization;
using BotFramework.Host.Workflows;
using Dapper;

namespace Games.Meta.Application.Tournaments;

public interface ITournamentWorkflow
{
    Task<TournamentCreateResult> CreateAsync(long chatId, long userId, string gameKey, int entryFee, int maxPlayers, CancellationToken ct);
    Task<TournamentJoinResult> JoinAsync(long tournamentId, long userId, long chatId, string displayName, CancellationToken ct);
    Task<bool> StartAsync(long tournamentId, long userId, CancellationToken ct);
    Task<TournamentReportResult> ReportMatchAsync(long matchId, long actorUserId, long victorUserId, CancellationToken ct);
    Task<TournamentPlayerInfo?> FinishAsync(long tournamentId, long actorUserId, long victorUserId, CancellationToken ct);
    Task<IReadOnlyList<TournamentPlayerInfo>?> CancelAsync(long tournamentId, long actorUserId, CancellationToken ct);
}

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

public sealed record TournamentCreateWorkflowCommand(
    string CommandId,
    string WorkflowId,
    long ChatId,
    long UserId,
    string GameKey,
    int EntryFee,
    int MaxPlayers) : IDurableWorkflowCommand;

public sealed record TournamentJoinWorkflowCommand(
    string CommandId,
    string WorkflowId,
    long TournamentId,
    long UserId,
    long ChatId,
    string DisplayName) : IDurableWorkflowCommand;

public sealed record TournamentStartWorkflowCommand(
    string CommandId,
    string WorkflowId,
    long TournamentId,
    long UserId) : IDurableWorkflowCommand;

public sealed record TournamentReportWorkflowCommand(
    string CommandId,
    string WorkflowId,
    long MatchId,
    long ActorUserId,
    long VictorUserId) : IDurableWorkflowCommand;

public sealed record TournamentFinishWorkflowCommand(
    string CommandId,
    string WorkflowId,
    long TournamentId,
    long ActorUserId,
    long VictorUserId) : IDurableWorkflowCommand;

public sealed record TournamentCancelWorkflowCommand(
    string CommandId,
    string WorkflowId,
    long TournamentId,
    long ActorUserId) : IDurableWorkflowCommand;

/// <summary>
/// Meta's adapter over the framework workflow primitive. It owns only command
/// ids, correlation and tournament-specific pending result shapes.
/// </summary>
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
        var workflowId = await ResolveWorkflowIdAsync(matchId, ct).ConfigureAwait(false);
        return await DispatchAsync(
            new TournamentReportWorkflowCommand(commandId, workflowId, matchId, actorUserId, victorUserId),
            new DurableWorkflowDispatchOptions(workflowId, commandId, "report", AggregateId: matchId.ToString(CultureInfo.InvariantCulture)),
            () => new TournamentReportResult(false, false, "Операция принята и выполняется.", Pending: true, CommandId: commandId),
            ct).ConfigureAwait(false);
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
        await using var connection = await connections.OpenAsync(ct).ConfigureAwait(false);
        var tournamentId = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT tournament_id FROM meta_tournament_matches WHERE id = @matchId",
            new { matchId },
            cancellationToken: ct)).ConfigureAwait(false);
        return TournamentWorkflowIds.ForTournament(tournamentId ?? matchId);
    }
}

internal static class TournamentWorkflowIds
{
    public static string ForTournament(long tournamentId) =>
        $"tournament:{tournamentId.ToString(CultureInfo.InvariantCulture)}";
}

public sealed class TournamentWorkflowHandler(
    TournamentCommandExecutor executor,
    IDurableWorkflowStepExecutor workflow)
{
    public Task<TournamentCreateResult> Handle(TournamentCreateWorkflowCommand command, CancellationToken ct) =>
        workflow.ExecuteAsync(
            command,
            new DurableWorkflowExecutionOptions(command.WorkflowId, command.CommandId, "create"),
            () => executor.CreateAsync(command.ChatId, command.UserId, command.GameKey, command.EntryFee, command.MaxPlayers, command.CommandId, ct),
            static result => result.Created,
            static result => result.Created,
            static result => result.Tournament?.Id.ToString(CultureInfo.InvariantCulture),
            static result => new { result.Created, result.Message, tournamentId = result.Tournament?.Id },
            ct);

    public Task<TournamentJoinResult> Handle(TournamentJoinWorkflowCommand command, CancellationToken ct) =>
        workflow.ExecuteAsync(
            command,
            new DurableWorkflowExecutionOptions(command.WorkflowId, command.CommandId, "join", command.TournamentId.ToString(CultureInfo.InvariantCulture)),
            () => executor.JoinAsync(command.TournamentId, command.UserId, command.ChatId, command.DisplayName, command.CommandId, ct),
            static result => result.Joined,
            static _ => false,
            _ => command.TournamentId.ToString(CultureInfo.InvariantCulture),
            static result => new { result.Joined, result.Message, tournamentId = result.Tournament?.Id },
            ct);

    public Task<bool> Handle(TournamentStartWorkflowCommand command, CancellationToken ct) =>
        workflow.ExecuteAsync(
            command,
            new DurableWorkflowExecutionOptions(command.WorkflowId, command.CommandId, "start", command.TournamentId.ToString(CultureInfo.InvariantCulture)),
            () => executor.StartAsync(command.TournamentId, command.UserId, command.CommandId, ct),
            static result => result,
            static _ => false,
            _ => command.TournamentId.ToString(CultureInfo.InvariantCulture),
            static result => new { started = result },
            ct);

    public Task<TournamentReportResult> Handle(TournamentReportWorkflowCommand command, CancellationToken ct) =>
        workflow.ExecuteAsync(
            command,
            new DurableWorkflowExecutionOptions(command.WorkflowId, command.CommandId, "report", command.MatchId.ToString(CultureInfo.InvariantCulture)),
            () => executor.ReportMatchAsync(command.MatchId, command.ActorUserId, command.VictorUserId, command.CommandId, ct),
            static result => result.Updated,
            static result => result.Finished,
            static result => result.Victor?.TournamentId.ToString(CultureInfo.InvariantCulture),
            result => new { result.Updated, result.Finished, result.Message, matchId = command.MatchId, command.VictorUserId },
            ct);

    public Task<TournamentPlayerInfo?> Handle(TournamentFinishWorkflowCommand command, CancellationToken ct) =>
        workflow.ExecuteAsync(
            command,
            new DurableWorkflowExecutionOptions(command.WorkflowId, command.CommandId, "finish", command.TournamentId.ToString(CultureInfo.InvariantCulture)),
            () => executor.FinishAsync(command.TournamentId, command.ActorUserId, command.VictorUserId, command.CommandId, ct),
            static result => result is not null,
            static result => result is not null,
            static result => result?.TournamentId.ToString(CultureInfo.InvariantCulture),
            result => new { completed = result is not null, tournamentId = command.TournamentId, command.VictorUserId },
            ct);

    public Task<IReadOnlyList<TournamentPlayerInfo>?> Handle(TournamentCancelWorkflowCommand command, CancellationToken ct) =>
        workflow.ExecuteAsync(
            command,
            new DurableWorkflowExecutionOptions(command.WorkflowId, command.CommandId, "cancel", command.TournamentId.ToString(CultureInfo.InvariantCulture)),
            () => executor.CancelAsync(command.TournamentId, command.ActorUserId, command.CommandId, ct),
            static result => result is not null,
            static result => result is not null,
            static _ => null,
            result => new { cancelled = result is not null, tournamentId = command.TournamentId, refundedPlayers = result?.Count ?? 0 },
            ct);
}
