using ChatAdministration.Application.Commands;
using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;

namespace ChatAdministration.Application.Services;

public sealed class ModerationCommandService(
    IChatAdministrationStore store,
    IModerationRuleProvider? ruleProvider = null,
    IModerationRateLimitStore? rateLimitStore = null)
{
    public async Task<ModerationCommandResult> ExecuteAutomaticAsync(
        NormalizedMessage message,
        ChatMemberRole observedRole,
        string displayName,
        string? username,
        CancellationToken ct)
    {
        await store.RecordMessageAsync(ToMessageIndexEntry(message, username, displayName), ct);
        var context = await store.LoadContextAsync(
            message.ChatId,
            message.AuthorId,
            message.AuthorId,
            observedRole,
            observedRole,
            displayName,
            displayName,
            ct);
        var observation = rateLimitStore is null
            ? new ModerationRateObservation(new RateLimitSnapshot(), new ModerationHistorySummary())
            : await rateLimitStore.RecordAsync(message, context.Chat.Settings.FloodPolicy.Window, ct);
        var messageContext = new ModerationMessageContext
        {
            Chat = context.Chat,
            Author = context.Target,
            Message = message,
            History = observation.History,
            RateLimits = observation.RateLimits,
        };
        var decision = AutomatedModerationPolicy.Decide(
            messageContext,
            ruleProvider?.GetRules(context.Chat) ?? []);
        if (!decision.Accepted)
            return new ModerationCommandResult(false, false, decision.ErrorCode, null, string.Empty);

        var commandId = $"automod:{message.ChatId}:{message.MessageId}";
        var responseText = decision.EffectPlan.Effects
            .Select(planned => planned.Effect)
            .OfType<SendMessageEffect>()
            .Select(effect => effect.Text)
            .FirstOrDefault() ?? string.Empty;
        var result = await store.PersistAsync(
            new PersistModerationCommand(
                commandId,
                commandId,
                decision.Case!.CorrelationId,
                $"telegram-message:{message.MessageId}",
                decision.Case,
                context.Actor,
                WithDesiredRestriction(context.Target, decision.Case),
                decision.Events,
                decision.EffectPlan,
                responseText,
                message.SentAt),
            ct);
        return new ModerationCommandResult(true, result.Duplicate, null, result.CaseId, responseText);
    }

    private static MessageIndexEntry ToMessageIndexEntry(
        NormalizedMessage message,
        string? username,
        string displayName) => new(
        message.ChatId,
        message.MessageId,
        message.AuthorId,
        message.ContentType,
        message.Entities.Any(entity => entity.Type is MessageEntityType.Url or MessageEntityType.TextLink),
        message.SentAt,
        string.IsNullOrEmpty(message.Text)
            ? null
            : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(message.Text))),
        username,
        displayName);

    public async Task<ModerationCommandResult> ExecuteManualAsync(
        ManualModerationCommand command,
        CancellationToken ct)
    {
        var context = await store.LoadContextAsync(
            command.ChatId,
            command.ActorUserId,
            command.TargetUserId,
            command.ActorObservedRole,
            command.TargetObservedRole,
            command.ActorDisplayName,
            command.TargetDisplayName,
            ct);

        var decision = ManualModerationPolicy.Decide(
            new ManualModerationRequest(
                command.ChatId,
                command.ActorUserId,
                command.TargetUserId,
                command.Action,
                command.Duration,
                command.Reason,
                command.SourceMessageId,
                command.CorrelationId,
                command.CausationId,
                command.CreatedAt),
            context.Chat,
            context.Actor,
            context.Target);

        if (!decision.Accepted)
        {
            var response = ResponseForError(decision.ErrorCode!);
            await store.EnqueueResponseAsync(command.ChatId, response, command.SourceMessageId, ct);
            return new ModerationCommandResult(false, false, decision.ErrorCode, null, response);
        }

        var responseText = decision.EffectPlan.Effects
            .Select(planned => planned.Effect)
            .OfType<SendMessageEffect>()
            .Select(effect => effect.Text)
            .FirstOrDefault() ?? "Модерационное действие принято.";
        var result = await store.PersistAsync(
            new PersistModerationCommand(
                command.CommandId,
                command.IdempotencyKey,
                command.CorrelationId,
                command.CausationId,
                decision.Case!,
                context.Actor,
                WithDesiredRestriction(context.Target, decision.Case!),
                decision.Events,
                decision.EffectPlan,
                responseText,
                command.CreatedAt,
                decision.Warning),
            ct);

        return new ModerationCommandResult(
            true,
            result.Duplicate,
            null,
            result.CaseId,
            result.Duplicate ? "Команда уже обработана." : responseText);
    }

    public async Task<ModerationCommandResult> ExecuteMuteAsync(
        MuteMemberCommand command,
        CancellationToken ct)
    {
        if (command.Duration <= TimeSpan.Zero)
            return await RejectAsync(command, "invalid_duration", "Укажите положительную длительность мута.", ct);

        var context = await store.LoadContextAsync(
            command.ChatId,
            command.ActorUserId,
            command.TargetUserId,
            command.ActorObservedRole,
            command.TargetObservedRole,
            command.ActorDisplayName,
            command.TargetDisplayName,
            ct);

        var decision = MutePolicy.Decide(
            new MuteRequest(
                command.ChatId,
                command.ActorUserId,
                command.TargetUserId,
                command.Duration,
                command.Reason,
                command.CorrelationId,
                command.CausationId,
                command.CreatedAt,
                command.SourceMessageId),
            context.Chat,
            context.Actor,
            context.Target);

        if (!decision.Accepted)
        {
            var message = decision.ErrorCode switch
            {
                "permission_denied" => "🚫 Недостаточно прав для этой команды.",
                "target_role_too_high" or "owner_protected" => "🚫 Нельзя модерировать пользователя с равной или более высокой ролью.",
                "moderation_disabled" => "🚫 Модерация отключена в этом чате.",
                "reason_required" => "Укажите причину мута.",
                _ => "Не удалось создать moderation case.",
            };
            return await RejectAsync(command, decision.ErrorCode!, message, ct);
        }

        var result = await store.PersistAsync(
            new PersistModerationCommand(
                command.CommandId,
                command.IdempotencyKey,
                command.CorrelationId,
                command.CausationId,
                decision.Case!,
                context.Actor,
                context.Target with { DesiredRestriction = decision.DesiredRestriction },
                decision.Events,
                decision.EffectPlan,
                decision.EffectPlan.Effects
                    .Select(planned => planned.Effect)
                    .OfType<SendMessageEffect>()
                    .Select(effect => effect.Text)
                    .FirstOrDefault() ?? "🔇 Мутация запланирована.",
                command.CreatedAt),
            ct);

        return new ModerationCommandResult(
            Accepted: true,
            Duplicate: result.Duplicate,
            ErrorCode: null,
            result.CaseId,
            result.Duplicate ? "Команда уже обработана." : "🔇 Мутация принята и будет применена.");
    }

    private async Task<ModerationCommandResult> RejectAsync(
        MuteMemberCommand command,
        string errorCode,
        string response,
        CancellationToken ct)
    {
        await store.EnqueueResponseAsync(command.ChatId, response, command.SourceMessageId, ct);
            return new ModerationCommandResult(false, false, errorCode, null, response);
    }

    private static string ResponseForError(string errorCode) => errorCode switch
    {
        "permission_denied" => "🚫 Недостаточно прав для этой команды.",
        "target_role_too_high" or "owner_protected" => "🚫 Нельзя модерировать пользователя с равной или более высокой ролью.",
        "moderation_disabled" => "🚫 Модерация отключена в этом чате.",
        "reason_required" => "Укажите причину модерационного действия.",
        "invalid_duration" => "Укажите положительную длительность бана.",
        "use_mute_policy" => "Использование: /mute 10m причина.",
        _ => "Не удалось создать moderation case.",
    };

    private static MemberState WithDesiredRestriction(MemberState member, ModerationCaseState moderationCase) =>
        moderationCase.Action switch
        {
            ModerationAction.Mute => member with
            {
                DesiredRestriction = new RestrictionState
                {
                    CanSendMessages = false,
                    Until = moderationCase.ExpiresAt ?? moderationCase.CreatedAt.AddMinutes(10),
                },
            },
            ModerationAction.Unmute => member with { DesiredRestriction = null },
            _ => member,
        };
}
