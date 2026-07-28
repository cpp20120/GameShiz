using System.Globalization;
using System.Text.Json;
using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Execution;
using Dapper;

namespace Games.Meta.Application.Effects;

public sealed record TournamentCreateAtomicEffect(
    long SeasonId,
    long ChatId,
    long CreatedBy,
    string GameKey,
    int EntryFee,
    int MaxPlayers) : IAtomicEffect;
