using System.Globalization;
using BotFramework.Host.Workflows;

namespace Games.Meta.Application.Tournaments;

public sealed class TournamentWorkflowHandler
{
    private TournamentWorkflowHandler()
    {
    }

    public static Task<TournamentCreateResult> Handle(
        TournamentCreateWorkflowCommand command,
        TournamentCommandExecutor executor,
        IDurableWorkflowStepExecutor workflow,
        CancellationToken ct) =>
        workflow.ExecuteAsync(
            command,
            new DurableWorkflowExecutionOptions(command.WorkflowId, command.CommandId, "create"),
            () => executor.CreateAsync(command.ChatId, command.UserId, command.GameKey, command.EntryFee, command.MaxPlayers, command.CommandId, ct),
            static result => result.Created,
            static result => result.Created,
            static result => result.Tournament?.Id.ToString(CultureInfo.InvariantCulture),
            static result => new { result.Created, result.Message, tournamentId = result.Tournament?.Id },
            ct);
}
