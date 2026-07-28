using System.Globalization;
using BotFramework.Host.Workflows;

namespace Games.Meta.Application.Tournaments;

public sealed class TournamentJoinWorkflowHandler
{
    private TournamentJoinWorkflowHandler()
    {
    }

    public static Task<TournamentJoinResult> Handle(
        TournamentJoinWorkflowCommand command,
        TournamentCommandExecutor executor,
        IDurableWorkflowStepExecutor workflow,
        CancellationToken ct) =>
        workflow.ExecuteAsync(
            command,
            new DurableWorkflowExecutionOptions(command.WorkflowId, command.CommandId, "join", command.TournamentId.ToString(CultureInfo.InvariantCulture)),
            () => executor.JoinAsync(command.TournamentId, command.UserId, command.ChatId, command.DisplayName, command.CommandId, ct),
            static result => result.Joined,
            static _ => false,
            _ => command.TournamentId.ToString(CultureInfo.InvariantCulture),
            static result => new { result.Joined, result.Message, tournamentId = result.Tournament?.Id },
            ct);
}
