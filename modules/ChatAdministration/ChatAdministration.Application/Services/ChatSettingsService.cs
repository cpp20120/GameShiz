using System.Globalization;
using ChatAdministration.Application.Commands;
using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Services;

public sealed class ChatSettingsService(IChatAdministrationStore store)
{
    public async Task<string> ExecuteAsync(ChatSettingsCommand command, CancellationToken ct)
    {
        var context = await store.LoadContextAsync(
            command.ChatId,
            command.ActorUserId,
            command.ActorUserId,
            command.ActorObservedRole,
            command.ActorObservedRole,
            command.ActorDisplayName,
            command.ActorDisplayName,
            ct);
        if (!AuthorizationPolicy.HasPermission(context.Chat, context.Actor, Permission.ChatManageSettings))
        {
            const string denied = "🚫 Только администратор может изменять настройки чата.";
            await store.EnqueueResponseAsync(command.ChatId, denied, command.SourceMessageId, ct);
            return denied;
        }

        var settings = Apply(context.Chat.Settings, command.Key, command.Value);
        if (settings is null)
        {
            var current = Render(context.Chat.Settings);
            var rows = new List<IReadOnlyList<InlineKeyboardButtonSpec>>();
            foreach (var option in MenuOptions(context.Chat.Settings))
            {
                var token = await store.CreateSettingsCallbackAsync(command.ChatId, option.Key, option.Value, DateTimeOffset.UtcNow.AddMinutes(10), ct);
                rows.Add([new InlineKeyboardButtonSpec(option.Label, $"settings:{token}")]);
            }
            await store.EnqueueEffectAsync(
                new SendMessageEffect(
                    command.ChatId,
                    current,
                    command.SourceMessageId,
                    MessageParseMode.Html,
                    new InlineKeyboardSpec(rows)),
                $"settings-menu:{command.ChatId}:{command.ActorUserId}:{command.CommandId}",
                EffectImportance.BestEffort,
                ct);
            return current;
        }

        await store.UpdateChatSettingsAsync(command.ChatId, settings, command.ActorUserId, command.CorrelationId, ct);
        var response = $"✅ Настройка <code>{command.Key}</code> обновлена.";
        await store.EnqueueResponseAsync(command.ChatId, response, command.SourceMessageId, ct);
        return response;
    }

    private static ChatSettings? Apply(ChatSettings settings, string? key, string? value) => key switch
    {
        "captcha" => settings with { CaptchaPolicy = settings.CaptchaPolicy with { Enabled = string.Equals(value, "on", StringComparison.Ordinal) } },
        "automod" => settings with { AutoModerationEnabled = string.Equals(value, "on", StringComparison.Ordinal) },
        "flood" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit) => settings with { FloodPolicy = settings.FloodPolicy with { MaximumMessages = limit } },
        "links" => settings with { LinkPolicy = settings.LinkPolicy with { Mode = string.Equals(value, "deny", StringComparison.Ordinal) ? LinkPolicyMode.DenyAll : LinkPolicyMode.AllowAll } },
        "mentions" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mentions) => settings with { MentionSpamPolicy = settings.MentionSpamPolicy with { Enabled = true, MaximumMentions = mentions } },
        "forwarded" => settings with { ForwardedMessagePolicy = settings.ForwardedMessagePolicy with { Enabled = string.Equals(value, "on", StringComparison.Ordinal) } },
        "newmember" => settings with { NewMemberPolicy = settings.NewMemberPolicy with { Enabled = string.Equals(value, "on", StringComparison.Ordinal) } },
        "commandspam" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var commands) => settings with { CommandSpamPolicy = settings.CommandSpamPolicy with { Enabled = true, MaximumCommands = commands } },
        "welcome" => settings with { WelcomeEnabled = string.Equals(value, "on", StringComparison.Ordinal) },
        "goodbye" => settings with { GoodbyeEnabled = string.Equals(value, "on", StringComparison.Ordinal) },
        "logchat" when string.Equals(value, "off", StringComparison.Ordinal) => settings with { ModerationLogChatId = null },
        "logchat" when long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var logChatId) => settings with { ModerationLogChatId = new ChatId(logChatId) },
        _ => null,
    };

    private static string Render(ChatSettings settings) =>
        $"⚙️ Настройки\nCaptcha: {(settings.CaptchaPolicy.Enabled ? "on" : "off")}\nAutomod: {(settings.AutoModerationEnabled ? "on" : "off")}\nFlood: {settings.FloodPolicy.MaximumMessages}\nLinks: {settings.LinkPolicy.Mode}\nMentions: {(settings.MentionSpamPolicy.Enabled ? settings.MentionSpamPolicy.MaximumMentions : 0)}\nForwarded: {(settings.ForwardedMessagePolicy.Enabled ? "on" : "off")}\nNew member: {(settings.NewMemberPolicy.Enabled ? "on" : "off")}\nCommand spam: {(settings.CommandSpamPolicy.Enabled ? settings.CommandSpamPolicy.MaximumCommands : 0)}\nWelcome: {(settings.WelcomeEnabled ? "on" : "off")}\nGoodbye: {(settings.GoodbyeEnabled ? "on" : "off")}\nLog chat: {settings.ModerationLogChatId?.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "same chat"}";

    private static IReadOnlyList<(string Label, string Key, string Value)> MenuOptions(ChatSettings settings) =>
    [
        ($"Captcha: {(settings.CaptchaPolicy.Enabled ? "off" : "on")}", "captcha", settings.CaptchaPolicy.Enabled ? "off" : "on"),
        ($"Automod: {(settings.AutoModerationEnabled ? "off" : "on")}", "automod", settings.AutoModerationEnabled ? "off" : "on"),
        ($"Forwarded: {(settings.ForwardedMessagePolicy.Enabled ? "off" : "on")}", "forwarded", settings.ForwardedMessagePolicy.Enabled ? "off" : "on"),
        ($"New member: {(settings.NewMemberPolicy.Enabled ? "off" : "on")}", "newmember", settings.NewMemberPolicy.Enabled ? "off" : "on"),
        ($"Welcome: {(settings.WelcomeEnabled ? "off" : "on")}", "welcome", settings.WelcomeEnabled ? "off" : "on"),
        ($"Goodbye: {(settings.GoodbyeEnabled ? "off" : "on")}", "goodbye", settings.GoodbyeEnabled ? "off" : "on"),
    ];
}
