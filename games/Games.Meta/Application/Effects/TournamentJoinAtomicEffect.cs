using System.Globalization;
using System.Text.Json;
using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Execution;
using Dapper;

namespace Games.Meta.Application.Effects;

public sealed record TournamentJoinAtomicEffect(
    long TournamentId,
    long UserId,
    long ChatId,
    string DisplayName,
    bool WalletAlreadyApplied = false) : IAtomicEffect;
