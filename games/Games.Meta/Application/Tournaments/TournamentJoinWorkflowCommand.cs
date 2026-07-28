using System.Globalization;
using BotFramework.Host.Workflows;
using Dapper;

namespace Games.Meta.Application.Tournaments;

public sealed record TournamentJoinWorkflowCommand(
    string CommandId,
    string WorkflowId,
    long TournamentId,
    long UserId,
    long ChatId,
    string DisplayName) : IDurableWorkflowCommand;
