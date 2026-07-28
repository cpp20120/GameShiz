using System.Globalization;
using System.Text.Json;
using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Execution;
using Dapper;

namespace Games.Meta.Application.Effects;

public sealed record TournamentFinishAtomicEffect(long TournamentId, long ActorUserId, long VictorUserId, bool PrizeAlreadyPaid = false) : IAtomicEffect;
