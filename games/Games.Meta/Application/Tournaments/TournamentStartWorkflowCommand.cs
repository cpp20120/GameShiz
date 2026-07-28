using System.Globalization;
using BotFramework.Host.Workflows;
using Dapper;

namespace Games.Meta.Application.Tournaments;

public sealed record TournamentStartWorkflowCommand(
    string CommandId,
    string WorkflowId,
    long TournamentId,
    long UserId) : IDurableWorkflowCommand;
