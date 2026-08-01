using System.Net;
using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;
using BotFramework.Host.Composition.Builder;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.Extensions.Logging;
using DomainChatId = ChatAdministration.Domain.Models.ChatId;
using DomainUserId = ChatAdministration.Domain.Models.UserId;

namespace ChatAdministration.Telegram.Infrastructure;

public sealed class TelegramEffectExecutor(
    ITelegramBotClient bot,
    IOptions<BotFrameworkOptions> botOptions,
    ILogger<TelegramEffectExecutor>? logger = null)
{
    private readonly BotFrameworkOptions options = botOptions.Value;

    public async Task ExecuteAsync(StoredModerationEffect stored, CancellationToken ct)
    {
        switch (stored.Payload)
        {
            case RestrictMemberEffect restrict:
                await ExecuteRestrictionAsync(
                    restrict.ChatId,
                    restrict.UserId,
                    new ChatPermissions { CanSendMessages = false },
                    restrict.Until,
                    ct);
                return;
            case UnrestrictMemberEffect unrestrict:
                await ExecuteRestrictionAsync(
                    unrestrict.ChatId,
                    unrestrict.UserId,
                    new ChatPermissions(true),
                    until: null,
                    ct: ct);
                return;
            case BanMemberEffect ban:
                await EnsureModerationPermissionAsync(ban.ChatId, ct);
                await bot.BanChatMember(
                    ban.ChatId.Value,
                    ban.UserId.Value,
                    untilDate: ban.Until?.UtcDateTime,
                    revokeMessages: false,
                    cancellationToken: ct);
                return;
            case UnbanMemberEffect unban:
                await EnsureModerationPermissionAsync(unban.ChatId, ct);
                await bot.UnbanChatMember(
                    unban.ChatId.Value,
                    unban.UserId.Value,
                    onlyIfBanned: false,
                    cancellationToken: ct);
                return;
            case KickMemberEffect kick:
                await EnsureModerationPermissionAsync(kick.ChatId, ct);
                await bot.BanChatMember(
                    kick.ChatId.Value,
                    kick.UserId.Value,
                    revokeMessages: false,
                    cancellationToken: ct);
                await bot.UnbanChatMember(
                    kick.ChatId.Value,
                    kick.UserId.Value,
                    onlyIfBanned: false,
                    cancellationToken: ct);
                return;
            case DeleteMessageEffect delete:
                await EnsureBotPermissionAsync(delete.ChatId, "delete", ct);
                await bot.DeleteMessage(delete.ChatId.Value, delete.MessageId, ct);
                return;
            case DeleteMessagesEffect deleteMessages:
                await EnsureBotPermissionAsync(deleteMessages.ChatId, "delete", ct);
                await ExecuteDeleteMessagesAsync(deleteMessages, ct);
                return;
            case EditMessageEffect edit:
                await bot.EditMessageText(
                    edit.ChatId.Value,
                    edit.MessageId,
                    edit.Text,
                    parseMode: edit.ParseMode == MessageParseMode.Html ? ParseMode.Html : ParseMode.None,
                    replyMarkup: ToMarkup(edit.InlineKeyboard),
                    cancellationToken: ct);
                return;
            case PinMessageEffect pin:
                await EnsureBotPermissionAsync(pin.ChatId, "pin", ct);
                await bot.PinChatMessage(
                    pin.ChatId.Value,
                    pin.MessageId,
                    disableNotification: pin.DisableNotification,
                    cancellationToken: ct);
                return;
            case UnpinMessageEffect unpin:
                await EnsureBotPermissionAsync(unpin.ChatId, "pin", ct);
                await bot.UnpinChatMessage(
                    unpin.ChatId.Value,
                    messageId: unpin.MessageId,
                    cancellationToken: ct);
                return;
            case AnswerCallbackQueryEffect answer:
                await bot.AnswerCallbackQuery(answer.CallbackQueryId, answer.Text, answer.ShowAlert, cancellationToken: ct);
                return;
            case GetChatMemberEffect member:
                await bot.GetChatMember(member.ChatId.Value, member.UserId.Value, ct);
                return;
            case GetChatAdministratorsEffect administrators:
                await bot.GetChatAdministrators(administrators.ChatId.Value, returnBots: true, cancellationToken: ct);
                return;
            case GetBotPermissionsEffect permissions:
                var me = await bot.GetMe(ct);
                await bot.GetChatMember(permissions.ChatId.Value, me.Id, ct);
                return;
            case SendMessageEffect message:
                await ExecuteSendMessageAsync(message, ct);
                return;
            case SendModerationLogEffect log:
                await ExecuteSendMessageAsync(new SendMessageEffect(log.ChatId, log.Text, ParseMode: MessageParseMode.Html), ct);
                return;
            case EmitMetricEffect metric:
                ModerationMetrics.Record(metric);
                return;
            case NotifyAdministratorsEffect notification:
                await NotifyAdministratorsAsync(notification, ct);
                return;
            case EmitTraceEventEffect trace:
                logger?.LogDebug("chat_admin.trace {Name} correlation={CorrelationId}", trace.Name, trace.CorrelationId);
                return;
            case WriteStructuredLogEffect structured:
                logger?.Log(
                    ParseLogLevel(structured.Level),
                    "chat_admin.event {EventName} correlation={CorrelationId} properties={Properties}",
                    structured.EventName,
                    structured.CorrelationId,
                    structured.Properties);
                return;
            case MarkModerationCaseRevokedEffect:
                throw new PermanentTelegramEffectException(
                    "persistence_effect_not_executable",
                    "Case transition effects are handled by the durable worker.");
            default:
                throw new PermanentTelegramEffectException("unknown_effect", $"Unknown effect type '{stored.EffectType}'.");
        }
    }

    private async Task ExecuteRestrictionAsync(
        DomainChatId chatId,
        DomainUserId userId,
        ChatPermissions permissions,
        DateTimeOffset? until,
        CancellationToken ct)
    {
        var me = await bot.GetMe(ct);
        var botMember = await bot.GetChatMember(chatId.Value, me.Id, ct);
        if (botMember.Status == ChatMemberStatus.Creator)
        {
            // Chat owners have all moderation rights.
        }
        else if (botMember is not ChatMemberAdministrator administrator || !administrator.CanRestrictMembers)
        {
            throw new PermanentTelegramEffectException(
                "bot_permission_denied",
                "Бот не является администратором с правом ограничивать участников.");
        }

        await bot.RestrictChatMember(
            chatId.Value,
            userId.Value,
            permissions,
            useIndependentChatPermissions: true,
            untilDate: until?.UtcDateTime,
            cancellationToken: ct);
    }

    private async Task EnsureModerationPermissionAsync(DomainChatId chatId, CancellationToken ct)
    {
        var me = await bot.GetMe(ct);
        var botMember = await bot.GetChatMember(chatId.Value, me.Id, ct);
        if (botMember.Status == ChatMemberStatus.Creator)
            return;
        if (botMember is not ChatMemberAdministrator administrator || !administrator.CanRestrictMembers)
            throw new PermanentTelegramEffectException(
                "bot_permission_denied",
                "Бот не является администратором с правом банить и ограничивать участников.");
    }

    private async Task EnsureBotPermissionAsync(DomainChatId chatId, string permission, CancellationToken ct)
    {
        var me = await bot.GetMe(ct);
        var botMember = await bot.GetChatMember(chatId.Value, me.Id, ct);
        if (botMember.Status == ChatMemberStatus.Creator)
            return;
        if (botMember is not ChatMemberAdministrator administrator)
            throw new PermanentTelegramEffectException(
                "bot_permission_denied",
                "Бот не является администратором этого чата.");

        var allowed = permission switch
        {
            "delete" => administrator.CanDeleteMessages,
            "pin" => administrator.CanPinMessages,
            _ => false,
        };
        if (!allowed)
            throw new PermanentTelegramEffectException(
                "bot_permission_denied",
                $"У бота отсутствует Telegram permission '{permission}'.");
    }

    private async Task ExecuteSendMessageAsync(SendMessageEffect effect, CancellationToken ct)
    {
        var reply = effect.ReplyToMessageId is { } messageId
            ? new ReplyParameters { MessageId = messageId }
            : null;
        await bot.SendMessage(
            effect.ChatId.Value,
            effect.Text,
            parseMode: effect.ParseMode == MessageParseMode.Html ? ParseMode.Html : ParseMode.None,
            replyParameters: reply,
            replyMarkup: ToMarkup(effect.InlineKeyboard),
            cancellationToken: ct);
    }

    private static InlineKeyboardMarkup? ToMarkup(InlineKeyboardSpec? keyboard) => keyboard is null
        ? null
        : new InlineKeyboardMarkup(keyboard.Rows
            .Select(row => row.Select(button => InlineKeyboardButton.WithCallbackData(button.Text, button.CallbackData)).ToArray())
            .ToArray());

    private static LogLevel ParseLogLevel(string value) =>
        Enum.TryParse<LogLevel>(value, true, out var level) ? level : LogLevel.Information;

    private async Task ExecuteDeleteMessagesAsync(DeleteMessagesEffect effect, CancellationToken ct)
    {
        foreach (var messageId in effect.MessageIds.Distinct())
        {
            try
            {
                await bot.DeleteMessage(effect.ChatId.Value, messageId, ct);
            }
            catch (Exception exception) when (Classify(exception).Outcome == TelegramEffectOutcome.AlreadyApplied)
            {
                // Deleting an already deleted message is an idempotent success.
            }
        }
    }

    private async Task NotifyAdministratorsAsync(NotifyAdministratorsEffect effect, CancellationToken ct)
    {
        // Moderation failures are operational alerts. They must not be
        // broadcast to every Telegram administrator of the affected chat:
        // apart from leaking internal details, most admins have not opened a
        // private conversation with the bot and would only create 400/403
        // noise. Bot:Admins is the explicit notification allow-list.
        var adminIds = options.Admins
            .Where(userId => userId > 0)
            .Distinct()
            .ToArray();
        foreach (var adminId in adminIds)
        {
            try
            {
                await bot.SendMessage(adminId, effect.Text, cancellationToken: ct);
            }
            catch (ApiRequestException exception) when (exception.ErrorCode is 400 or 403)
            {
                // A user may have disabled private messages. One inaccessible
                // administrator must not turn the whole best-effort notification
                // into a retry storm.
                logger?.LogDebug(
                    exception,
                    "chat_admin.admin_notification_unavailable chat={ChatId} user={UserId}",
                    effect.ChatId.Value,
                    adminId);
            }
        }
    }

    public static TelegramEffectFailure Classify(Exception exception)
    {
        if (exception is PermanentTelegramEffectException permanent)
            return new(TelegramEffectOutcome.Permanent, permanent.Code, permanent.Message);

        if (exception is ApiRequestException api)
        {
            var description = api.Message ?? "Telegram API error";
            var lower = description.ToLowerInvariant();
            if (lower.Contains("message to delete not found", StringComparison.Ordinal)
                || lower.Contains("message can't be deleted", StringComparison.Ordinal)
                || lower.Contains("already banned", StringComparison.Ordinal)
                || lower.Contains("user is not a member", StringComparison.Ordinal)
                || lower.Contains("not enough rights to unban", StringComparison.Ordinal))
                return new(TelegramEffectOutcome.AlreadyApplied, "already_applied", description);
            if (api.ErrorCode == 429)
            {
                var retryAfter = api.Parameters?.RetryAfter is { } seconds
                    ? TimeSpan.FromSeconds(seconds)
                    : (TimeSpan?)null;
                return new(TelegramEffectOutcome.Retryable, "rate_limited", description, retryAfter);
            }
            if (api.ErrorCode >= 500)
                return new(TelegramEffectOutcome.Retryable, "telegram_server_error", description);
            if (lower.Contains("not enough rights", StringComparison.Ordinal)
                || lower.Contains("administrator", StringComparison.Ordinal)
                || lower.Contains("permission", StringComparison.Ordinal)
                || lower.Contains("chat not found", StringComparison.Ordinal)
                || lower.Contains("user not found", StringComparison.Ordinal)
                || lower.Contains("can't restrict", StringComparison.Ordinal))
                return new(TelegramEffectOutcome.Permanent, "telegram_permission_or_target", description);
            return new(TelegramEffectOutcome.Permanent, "telegram_bad_request", description);
        }

        if (exception is RequestException request)
        {
            var statusCode = request.HttpStatusCode is { } status ? (int)status : 0;
            return statusCode >= 500
                ? new(TelegramEffectOutcome.Retryable, "telegram_transport_server_error", request.Message)
                : new(TelegramEffectOutcome.Unknown, "telegram_transport_unknown", request.Message);
        }

        if (exception is HttpRequestException or TimeoutException or TaskCanceledException)
            return new(TelegramEffectOutcome.Unknown, "telegram_timeout", exception.Message);

        return new(TelegramEffectOutcome.Unknown, "effect_execution_unknown", exception.Message);
    }

}
