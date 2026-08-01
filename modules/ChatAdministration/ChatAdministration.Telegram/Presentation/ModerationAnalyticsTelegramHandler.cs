using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Models;
using BotFramework.Host.Composition.Builder;
using BotFramework.Sdk.UpdateHandling;
using BotFramework.Sdk.UpdateHandling.Routes;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;

namespace ChatAdministration.Telegram.Presentation;

[Command("/modstats")]
public sealed class ModerationAnalyticsTelegramHandler(
    ModerationAnalyticsService service,
    IChatAdministrationStore store,
    IOptions<BotFrameworkOptions> botOptions) : IUpdateHandler
{
    private readonly BotFrameworkOptions options = botOptions.Value;

    public async Task HandleAsync(UpdateContext ctx)
    {
        var message = ctx.Update.Message;
        if (message?.From is null)
            return;
        var role = await TelegramRoleResolver.ResolveAsync(ctx.Bot, options, message.Chat.Id, message.From.Id, ctx.Ct);
        var result = await service.ExecuteAsync(
            new ChatId(message.Chat.Id),
            new UserId(message.From.Id),
            role,
            DisplayName(message.From.FirstName, message.From.LastName, message.From.Username, message.From.Id),
            ctx.Ct);
        var text = result is null
            ? "🚫 Недостаточно прав для просмотра статистики."
            : $"📊 Статистика модерации\nCases: {result.Cases}\nApplied: {result.AppliedCases}\nFailed: {result.FailedCases}\nUnknown: {result.UnknownCases}\nActive warnings: {result.ActiveWarnings}\nIndexed messages: {result.IndexedMessages}";
        await store.EnqueueResponseAsync(new ChatId(message.Chat.Id), text, message.MessageId, ctx.Ct);
    }

    private static string DisplayName(string firstName, string? lastName, string? username, long id) =>
        string.Join(' ', new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value)))
        is { Length: > 0 } name
            ? name
            : username ?? $"User {id}";
}
