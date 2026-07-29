using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;
using ChatAdministration.Application.Services;
using BotFramework.Sdk.Modules;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ChatAdministration.Telegram.Infrastructure;

public sealed partial class ModerationEffectWorker(
    IChatAdministrationStore store,
    TelegramEffectExecutor executor,
    ITelegramBotClient bot,
    ILogger<ModerationEffectWorker> logger) : IBackgroundJob
{
    public string Name => "chat_administration.effect_worker";

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            var due = await store.ClaimDueEffectsAsync(20, TimeSpan.FromMinutes(2), stoppingToken);
            foreach (var effect in due)
            {
                processed++;
                await ExecuteClaimedAsync(effect, stoppingToken);
            }

            await ReconcileUnknownAsync(stoppingToken);
            await Task.Delay(processed == 0 ? TimeSpan.FromSeconds(2) : TimeSpan.FromMilliseconds(100), stoppingToken);
        }
    }

    private async Task ExecuteClaimedAsync(StoredModerationEffect effect, CancellationToken ct)
    {
        try
        {
            if (effect.Payload is ScheduleEffect schedule)
            {
                await store.EnqueueScheduledEffectAsync(
                    schedule.Effect.Payload,
                    schedule.ExecuteAt,
                    $"scheduled:{schedule.Effect.Id}:{schedule.ExecuteAt.UtcTicks}",
                    EffectImportance.Required,
                    ct);
                await store.MarkEffectAppliedAsync(effect, ct);
                return;
            }

            if (effect.Payload is CancelScheduledEffect cancel)
            {
                await store.CancelEffectAsync(cancel.ScheduledEffectId, ct);
                await store.MarkEffectAppliedAsync(effect, ct);
                return;
            }

            if (effect.Payload is PersistAggregateEffect
                or AppendDomainEventsEffect
                or CreateModerationCaseEffect
                or UpdateModerationCaseEffect
                or SaveVerificationSessionEffect
                or WriteAuditEventEffect)
            {
                // These contracts are for atomic command plans. Their durable
                // state is committed together with the outbox row, so a later
                // worker pass must not write the same aggregate a second time.
                await store.MarkEffectAppliedAsync(effect, ct);
                return;
            }

            if (effect.Payload is MarkModerationCaseRevokedEffect)
            {
                await store.MarkEffectAppliedAsync(effect, ct);
                return;
            }
            await executor.ExecuteAsync(effect, ct);
            await store.MarkEffectAppliedAsync(effect, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            var failure = TelegramEffectExecutor.Classify(ex);
            switch (failure.Outcome)
            {
                case TelegramEffectOutcome.Applied:
                case TelegramEffectOutcome.AlreadyApplied:
                    await store.MarkEffectAppliedAsync(effect, ct);
                    break;
                case TelegramEffectOutcome.Retryable:
                    await store.MarkEffectFailedAsync(effect, failure.Code, failure.Message, true, failure.RetryAfter, ct);
                    break;
                case TelegramEffectOutcome.Permanent:
                    await store.MarkEffectFailedAsync(effect, failure.Code, failure.Message, false, null, ct);
                    if (failure.Code == "bot_permission_denied" && TryGetChatId(effect.Payload, out var chatId))
                    {
                        await store.EnqueueResponseAsync(
                            chatId,
                            "🚫 Бот не имеет нужного Telegram permission для этого действия. Проверьте права администратора.",
                            null,
                            ct);
                    }
                    LogPermanentFailure(effect.EffectId.Value, failure.Code, failure.Message);
                    break;
                case TelegramEffectOutcome.Unknown:
                    await store.MarkEffectUnknownAsync(effect, failure.Code, failure.Message, ct);
                    LogUnknownFailure(effect.EffectId.Value, failure.Code);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected Telegram effect outcome '{failure.Outcome}'.");
            }
        }
    }

    private static bool TryGetChatId(ModerationEffect effect, out ChatAdministration.Domain.Models.ChatId chatId)
    {
        chatId = effect switch
        {
            RestrictMemberEffect value => value.ChatId,
            UnrestrictMemberEffect value => value.ChatId,
            BanMemberEffect value => value.ChatId,
            UnbanMemberEffect value => value.ChatId,
            KickMemberEffect value => value.ChatId,
            DeleteMessageEffect value => value.ChatId,
            DeleteMessagesEffect value => value.ChatId,
            SendMessageEffect value => value.ChatId,
            SendModerationLogEffect value => value.ChatId,
            _ => default,
        };
        return chatId != default;
    }

    private async Task ReconcileUnknownAsync(CancellationToken ct)
    {
        var unknown = await store.ListUnknownEffectsAsync(20, ct);
        foreach (var effect in unknown)
        {
            if (!TryGetMemberTarget(effect.Payload, out var chatId, out var userId))
            {
                if (CanSafelyRetryUnknown(effect.Payload))
                    await store.RequeueUnknownAsync(effect, ct);
                continue;
            }

            try
            {
                var member = await bot.GetChatMember(chatId.Value, userId.Value, ct);
                var applied = IsApplied(effect.Payload, member);
                if (applied) await store.ConfirmUnknownAppliedAsync(effect, ct);
                else await store.RequeueUnknownAsync(effect, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                LogReconciliationFailed(effect.EffectId.Value, ex);
            }
        }
    }

    private static bool TryGetMemberTarget(
        ModerationEffect effect,
        out ChatAdministration.Domain.Models.ChatId chatId,
        out ChatAdministration.Domain.Models.UserId userId)
    {
        (chatId, userId) = effect switch
        {
            RestrictMemberEffect value => (value.ChatId, value.UserId),
            UnrestrictMemberEffect value => (value.ChatId, value.UserId),
            BanMemberEffect value => (value.ChatId, value.UserId),
            UnbanMemberEffect value => (value.ChatId, value.UserId),
            KickMemberEffect value => (value.ChatId, value.UserId),
            _ => (default, default),
        };
        return chatId != default && userId != default;
    }

    private static bool IsApplied(ModerationEffect effect, ChatMember member) => effect switch
    {
        RestrictMemberEffect restriction => member is ChatMemberRestricted restricted
            && !restricted.CanSendMessages
            && (restricted.UntilDate is null
                || restricted.UntilDate >= restriction.Until.UtcDateTime.AddMinutes(-1)),
        UnrestrictMemberEffect => member switch
        {
            ChatMemberBanned => false,
            ChatMemberRestricted restricted => restricted.CanSendMessages
                || restricted.UntilDate is null
                || restricted.UntilDate <= DateTime.UtcNow,
            _ => true,
        },
        BanMemberEffect ban => member is ChatMemberBanned banned
            && (ban.Until is null
                || banned.UntilDate is { } until
                    && until >= ban.Until.Value.UtcDateTime.AddMinutes(-1)),
        UnbanMemberEffect => member is not ChatMemberBanned,
        KickMemberEffect => member is ChatMemberLeft,
        _ => false,
    };

    private static bool CanSafelyRetryUnknown(ModerationEffect effect) => effect switch
    {
        RestrictMemberEffect or UnrestrictMemberEffect or BanMemberEffect or UnbanMemberEffect or KickMemberEffect => true,
        DeleteMessageEffect or DeleteMessagesEffect or EditMessageEffect or PinMessageEffect or UnpinMessageEffect => true,
        GetChatMemberEffect or GetChatAdministratorsEffect or GetBotPermissionsEffect => true,
        _ => false,
    };

    [LoggerMessage(LogLevel.Error, "chat_admin.effect.permanent_failure effect={EffectId} code={Code} message={Message}")]
    partial void LogPermanentFailure(Guid effectId, string code, string message);

    [LoggerMessage(LogLevel.Warning, "chat_admin.effect.unknown effect={EffectId} code={Code}")]
    partial void LogUnknownFailure(Guid effectId, string code);

    [LoggerMessage(LogLevel.Warning, "chat_admin.effect.reconciliation_failed effect={EffectId}")]
    partial void LogReconciliationFailed(Guid effectId, Exception exception);
}
