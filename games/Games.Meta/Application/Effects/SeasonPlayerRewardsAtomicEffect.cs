using System.Globalization;
using System.Text.Json;
using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Execution;
using Dapper;
using Games.Meta.Application.Clans;
using Games.Meta.Application.Models;
using Games.Meta.Domain.Seasons;

namespace Games.Meta.Application.Effects;

public sealed record SeasonPlayerRewardsAtomicEffect(long SeasonId) : IAtomicEffect;
