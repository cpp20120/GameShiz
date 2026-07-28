using System.Globalization;
using BotFramework.Host.Workflows;
using Dapper;

namespace Games.Meta.Application.Tournaments;

public sealed record TournamentFinishWorkflowCommand(
    string CommandId,
    string WorkflowId,
    long TournamentId,
    long ActorUserId,
    long VictorUserId) : IDurableWorkflowCommand;
