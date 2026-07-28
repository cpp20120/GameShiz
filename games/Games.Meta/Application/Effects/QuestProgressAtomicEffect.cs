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

public sealed record QuestProgressAtomicEffect(
    long SeasonId,
    long ChatId,
    long UserId,
    GameCompletedMetaEvent Completion,
    DateTimeOffset Now) : IAtomicEffect;
