
using System.Globalization;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Games.Meta.Application.Effects;

namespace Games.Meta.Application.Quests;

public sealed class QuestService(
    IMetaService meta,
    IQuestStore quests,
    IAtomicEffectExecutor effects) : IQuestService
{
    public async Task<IReadOnlyList<QuestProgressUpdate>> ApplyGameCompletedAsync(GameCompletedMetaEvent ev, CancellationToken ct)
    {
        var season = await meta.GetActiveSeasonAsync(ct);
        return await effects.ExecuteAsync(
            new AtomicEffectExecutionEnvelope(
                "meta.quest",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"meta:quest:progress:{season.Id}:{ev.ChatId}:{ev.UserId}:{ev.OccurredAt}:{ev.GameKey}:{ev.Stake}:{ev.Payout}:{ev.IsWin}:{ev.Multiplier}"),
                $"{season.Id}:{ev.ChatId}:{ev.UserId}",
                [
                    $"game:meta.quest:{season.Id}:{ev.ChatId}:{ev.UserId}",
                    $"player:meta.season:{season.Id}:{ev.ChatId}:{ev.UserId}",
                    $"quest:{season.Id}:{ev.ChatId}:{ev.UserId}",
                ]),
            new AtomicEffectPlan<IReadOnlyList<QuestProgressUpdate>>(
                [],
                [new QuestProgressAtomicEffect(season.Id, ev.ChatId, ev.UserId, ev, DateTimeOffset.FromUnixTimeMilliseconds(ev.OccurredAt))],
                outputs => outputs.TryGetValue("updates", out var value)
                    ? (IReadOnlyList<QuestProgressUpdate>)value!
                    : []),
            ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PlayerQuestView>> GetQuestsAsync(long chatId, long userId, CancellationToken ct)
    {
        var season = await meta.GetActiveSeasonAsync(ct);
        return await quests.GetQuestsAsync(season, chatId, userId, DateTimeOffset.UtcNow, ct);
    }

    public async Task<QuestClaimResult?> ClaimAsync(long chatId, long userId, string displayName, string questId, CancellationToken ct)
    {
        var season = await meta.GetActiveSeasonAsync(ct);
        return await effects.ExecuteAsync(
            new AtomicEffectExecutionEnvelope(
                "meta.quest",
                $"meta:quest:claim:{season.Id}:{chatId}:{userId}:{questId}",
                $"{season.Id}:{chatId}:{userId}",
                [
                    $"game:meta.quest:{season.Id}:{chatId}:{userId}",
                    $"wallet:{chatId}:{userId}",
                    $"quest:{season.Id}:{chatId}:{userId}:{questId}",
                ]),
            new AtomicEffectPlan<QuestClaimResult?>(
                null,
                [new QuestClaimAtomicEffect(
                    season.Id,
                    chatId,
                    userId,
                    displayName,
                    questId,
                    DateTimeOffset.UtcNow)],
                outputs => outputs.TryGetValue("result", out var value)
                    ? (QuestClaimResult?)value
                    : null),
            ct).ConfigureAwait(false);
    }
}
