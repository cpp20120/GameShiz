using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public static class MemberLifecyclePolicy
{
    public static MemberLifecycleDecision Join(
        ChatState chat,
        MemberState member,
        bool verificationRequired,
        DateTimeOffset now)
    {
        if (!chat.IsEnabled)
            return MemberLifecycleDecision.Reject("chat_disabled");
        if (member.ChatId != chat.Id)
            return MemberLifecycleDecision.Reject("member_chat_mismatch");

        var effects = new List<PlannedEffect>();
        if (chat.Settings.WelcomeEnabled && !verificationRequired)
        {
            effects.Add(new PlannedEffect(
                new SendMessageEffect(chat.Id, Render(chat.Settings.WelcomeTemplate, chat, member, chat.Settings.RulesText), ParseMode: MessageParseMode.Html),
                EffectImportance.BestEffort,
                []));
        }

        return new MemberLifecycleDecision(
            true,
            null,
            [new MemberJoined(member, now)],
            new EffectPlan(effects));
    }

    public static MemberLifecycleDecision Leave(
        ChatState chat,
        UserId userId,
        string displayName,
        string? username,
        DateTimeOffset now)
    {
        if (!chat.IsEnabled)
            return MemberLifecycleDecision.Reject("chat_disabled");
        if (userId.Value == 0)
            return MemberLifecycleDecision.Reject("invalid_user");

        var effects = chat.Settings.GoodbyeEnabled
            ? new PlannedEffect[]
            {
                new(
                    new SendMessageEffect(chat.Id, Render(chat.Settings.GoodbyeTemplate, chat, displayName, chat.Settings.RulesText, username), ParseMode: MessageParseMode.Html),
                    EffectImportance.BestEffort,
                    []),
            }
            : [];

        return new MemberLifecycleDecision(
            true,
            null,
            [new MemberLeft(chat.Id, userId, displayName, now)],
            new EffectPlan(effects));
    }

    public static string RenderRules(ChatState chat) =>
        string.IsNullOrWhiteSpace(chat.Settings.RulesText)
            ? "Правила чата пока не настроены."
            : chat.Settings.RulesText.Trim();

    public static SendMessageEffect CreateWelcomeEffect(ChatState chat, MemberState member) =>
        new(chat.Id, Render(chat.Settings.WelcomeTemplate, chat, member, chat.Settings.RulesText), ParseMode: MessageParseMode.Html);

    private static string Render(string template, ChatState chat, MemberState member, string rules) =>
        Render(template, chat, member.DisplayName, rules, member.Username);

    private static string Render(string? template, ChatState chat, string displayName, string rules, string? username = null)
    {
        var normalizedTemplate = template is null ? "{user}" : template.Trim();
        if (normalizedTemplate.Length == 0)
            normalizedTemplate = "{user}";

        return normalizedTemplate
            .Replace("{user}", displayName, StringComparison.Ordinal)
            .Replace("{username}", username is null ? displayName : $"@{username.TrimStart('@')}", StringComparison.Ordinal)
            .Replace("{chat}", chat.Title, StringComparison.Ordinal)
            .Replace("{rules}", rules, StringComparison.Ordinal);
    }
}
