using System.Globalization;
using BotFramework.Host.Workflows;
using Dapper;

namespace Games.Meta.Application.Tournaments;

public sealed record TournamentCancelWorkflowCommand(
    string CommandId,
    string WorkflowId,
    long TournamentId,
    long ActorUserId) : IDurableWorkflowCommand;
