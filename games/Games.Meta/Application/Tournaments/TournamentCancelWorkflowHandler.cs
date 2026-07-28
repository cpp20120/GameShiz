using System.Globalization;
using BotFramework.Host.Workflows;

namespace Games.Meta.Application.Tournaments;

public sealed class TournamentCancelWorkflowHandler
{
    private TournamentCancelWorkflowHandler()
    {
    }

    public static Task<IReadOnlyList<TournamentPlayerInfo>?> Handle(
        TournamentCancelWorkflowCommand command,
        TournamentCommandExecutor executor,
        IDurableWorkflowStepExecutor workflow,
        CancellationToken ct) =>
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
