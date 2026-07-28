using System.Globalization;
using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Execution;
using Dapper;
using BotFramework.Sdk.Events.Meta;
using Games.Meta.Domain.Quests;
using Games.Meta.Domain.Seasons;
using Games.Meta.Infrastructure.Catalog;

namespace Games.Meta.Application.Effects;

public sealed record QuestClaimAtomicEffect(
    long SeasonId,
    long ChatId,
    long UserId,
    string DisplayName,
    string QuestId,
    DateTimeOffset Now) : IAtomicEffect;
