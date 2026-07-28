using System.Globalization;
using BotFramework.Host.Workflows;

namespace Games.Meta.Application.Tournaments;

public sealed class TournamentReportWorkflowHandler
{
    private TournamentReportWorkflowHandler()
    {
    }

    public static Task<TournamentReportResult> Handle(
        TournamentReportWorkflowCommand command,
        TournamentCommandExecutor executor,
        IDurableWorkflowStepExecutor workflow,
        CancellationToken ct) =>
        workflow.ExecuteAsync(
            command,
            new DurableWorkflowExecutionOptions(command.WorkflowId, command.CommandId, "report", command.MatchId.ToString(CultureInfo.InvariantCulture)),
            () => executor.ReportMatchAsync(command.MatchId, command.ActorUserId, command.VictorUserId, command.CommandId, ct),
            static result => result.Updated,
            static result => result.Finished,
            static result => result.Victor?.TournamentId.ToString(CultureInfo.InvariantCulture),
            result => new { result.Updated, result.Finished, result.Message, matchId = command.MatchId, command.VictorUserId },
            ct);
}
