using System.Globalization;
using BotFramework.Host.Workflows;
using Dapper;

namespace Games.Meta.Application.Tournaments;

public sealed record TournamentReportWorkflowCommand(
    string CommandId,
    string WorkflowId,
    long MatchId,
    long ActorUserId,
    long VictorUserId) : IDurableWorkflowCommand;
