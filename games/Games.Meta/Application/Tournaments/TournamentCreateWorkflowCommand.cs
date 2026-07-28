using System.Globalization;
using BotFramework.Host.Workflows;
using Dapper;

namespace Games.Meta.Application.Tournaments;

public sealed record TournamentCreateWorkflowCommand(
    string CommandId,
    string WorkflowId,
    long ChatId,
    long UserId,
    string GameKey,
    int EntryFee,
    int MaxPlayers) : IDurableWorkflowCommand;
