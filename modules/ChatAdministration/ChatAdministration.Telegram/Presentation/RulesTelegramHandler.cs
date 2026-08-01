using ChatAdministration.Application.Services;
using ChatAdministration.Application.Commands;
using ChatAdministration.Domain.Policies;
using ChatAdministration.Domain.Models;
using BotFramework.Host.Composition.Builder;
using BotFramework.Sdk.UpdateHandling;
using BotFramework.Sdk.UpdateHandling.Routes;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;

namespace ChatAdministration.Telegram.Presentation;

[Command("/rules")]
[Command("/rule")]
public sealed class RulesTelegramHandler(
    MemberLifecycleService lifecycle,
    ModerationRuleService rules,
    IChatAdministrationStore store,
    IOptions<BotFrameworkOptions> botOptions) : IUpdateHandler
{
    private readonly BotFrameworkOptions options = botOptions.Value;

    public async Task HandleAsync(UpdateContext ctx)
    {
        var message = ctx.Update.Message;
        if (message?.Text is null || message.From is null)
            return;

        var role = await TelegramRoleResolver.ResolveAsync(ctx.Bot, options, message.Chat.Id, message.From.Id, ctx.Ct);
        if (message.Text.StartsWith("/rule", StringComparison.OrdinalIgnoreCase)
            && !message.Text.StartsWith("/rules", StringComparison.OrdinalIgnoreCase))
        {
            var tokens = message.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length is < 3 or > 4
                || !bool.TryParse(tokens[2] switch
                {
                    "on" => "true",
                    "off" => "false",
                    _ => tokens[2],
                }, out var enabled)
                || !int.TryParse(tokens.ElementAtOrDefault(3), out var scoreOverride) && tokens.Length == 4)
            {
                await store.EnqueueResponseAsync(new ChatId(message.Chat.Id), "Использование: /rule <id> <on|off> [score]", message.MessageId, ctx.Ct);
                return;
            }

            var response = await rules.ExecuteAsync(
                new ModerationRuleCommand(
                    $"rule:{ctx.Update.Id}:{message.Chat.Id}:{message.MessageId}",
                    $"rule:{message.Chat.Id}:{message.MessageId}",
                    new ChatId(message.Chat.Id),
                    new UserId(message.From.Id),
                    role,
                    new ChatAdministration.Domain.Models.RuleId(tokens[1]),
                    enabled,
                    tokens.Length == 4 ? scoreOverride : null,
                    DateTimeOffset.UtcNow),
                ctx.Ct);
            await store.EnqueueResponseAsync(new ChatId(message.Chat.Id), response, message.MessageId, ctx.Ct);
            return;
        }

        var text = await lifecycle.RulesAsync(
            new ChatId(message.Chat.Id),
            new UserId(message.From.Id),
            role,
            DisplayName(message.From.FirstName, message.From.LastName, message.From.Username, message.From.Id),
            ctx.Ct);
        await store.EnqueueResponseAsync(new ChatId(message.Chat.Id), text, message.MessageId, ctx.Ct);
    }

    private static string DisplayName(string firstName, string? lastName, string? username, long id) =>
        string.Join(' ', new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value)))
        is { Length: > 0 } name
            ? name
            : username ?? $"User {id}";
}
