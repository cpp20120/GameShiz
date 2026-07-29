using ChatAdministration.Application.Commands;
using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Services;

public sealed class ModerationRuleService(IChatAdministrationStore store)
{
    public async Task<string> ExecuteAsync(ModerationRuleCommand command, CancellationToken ct)
    {
        var context = await store.LoadContextAsync(
            command.ChatId,
            command.ActorUserId,
            command.ActorUserId,
            command.ActorObservedRole,
            command.ActorObservedRole,
            "moderator",
            "moderator",
            ct);
        if (!AuthorizationPolicy.HasPermission(context.Chat, context.Actor, Permission.RulesManage))
            return "🚫 Недостаточно прав для изменения правил.";

        var type = RuleType(command.RuleId);
        if (type is null)
            return "ℹ️ Неизвестное правило. Используйте /rules для списка.";

        var rules = context.Chat.Settings.ModerationRules.ToDictionary(rule => rule.Id.Value, StringComparer.OrdinalIgnoreCase);
        rules[command.RuleId.Value] = new ModerationRuleDefinition
        {
            Id = command.RuleId,
            Type = type.Value,
            IsEnabled = command.Enabled,
            ScoreOverride = command.ScoreOverride,
        };
        await store.UpdateChatSettingsAsync(
            command.ChatId,
            context.Chat.Settings with { ModerationRules = rules.Values.ToArray() },
            command.ActorUserId,
            command.CorrelationId,
            ct);
        return $"✅ Правило <code>{command.RuleId}</code> {(command.Enabled ? "включено" : "выключено")}.";
    }

    private static ModerationRuleType? RuleType(RuleId id) => id.Value.ToLowerInvariant() switch
    {
        "flood" => ModerationRuleType.Flood,
        "duplicate_message" => ModerationRuleType.DuplicateMessage,
        "links" => ModerationRuleType.Link,
        "caps" => ModerationRuleType.Caps,
        "forbidden_words" => ModerationRuleType.ForbiddenWords,
        "mention_spam" => ModerationRuleType.MentionSpam,
        "forwarded_message" => ModerationRuleType.ForwardedMessage,
        "media_type" => ModerationRuleType.MediaType,
        "new_member" => ModerationRuleType.NewMember,
        "command_spam" => ModerationRuleType.CommandSpam,
        _ => null,
    };
}
