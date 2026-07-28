using System.Globalization;
using BotFramework.Host.Workflows;

namespace Games.Meta.Application.Tournaments;

public sealed class TournamentFinishWorkflowHandler
{
    private TournamentFinishWorkflowHandler()
    {
    }

    public static Task<TournamentPlayerInfo?> Handle(
        TournamentFinishWorkflowCommand command,
        TournamentCommandExecutor executor,
        IDurableWorkflowStepExecutor workflow,
        CancellationToken ct) =>
        workflow.ExecuteAsync(
            command,
            new DurableWorkflowExecutionOptions(command.WorkflowId, command.CommandId, "finish", command.TournamentId.ToString(CultureInfo.InvariantCulture)),
            () => executor.FinishAsync(command.TournamentId, command.ActorUserId, command.VictorUserId, command.CommandId, ct),
            static result => result is not null,
            static result => result is not null,
            static result => result?.TournamentId.ToString(CultureInfo.InvariantCulture),
            result => new { completed = result is not null, tournamentId = command.TournamentId, command.VictorUserId },
            ct);
}
