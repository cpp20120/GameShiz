using System.Globalization;
using BotFramework.Host.Workflows;

namespace Games.Meta.Application.Tournaments;

public sealed class TournamentStartWorkflowHandler
{
    private TournamentStartWorkflowHandler()
    {
    }

    public static Task<bool> Handle(
        TournamentStartWorkflowCommand command,
        TournamentCommandExecutor executor,
        IDurableWorkflowStepExecutor workflow,
        CancellationToken ct) =>
        workflow.ExecuteAsync(
            command,
            new DurableWorkflowExecutionOptions(command.WorkflowId, command.CommandId, "start", command.TournamentId.ToString(CultureInfo.InvariantCulture)),
            () => executor.StartAsync(command.TournamentId, command.UserId, command.CommandId, ct),
            static result => result,
            static _ => false,
            _ => command.TournamentId.ToString(CultureInfo.InvariantCulture),
            static result => new { started = result },
            ct);
}
