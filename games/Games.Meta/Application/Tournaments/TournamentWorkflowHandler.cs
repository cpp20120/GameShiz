using System.Globalization;
using BotFramework.Host.Workflows;
using Dapper;

namespace Games.Meta.Application.Tournaments;

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
