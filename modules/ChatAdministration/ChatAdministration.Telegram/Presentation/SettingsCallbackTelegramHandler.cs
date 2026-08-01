using ChatAdministration.Application.Commands;
using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;
using BotFramework.Host.Composition.Builder;
using BotFramework.Sdk.UpdateHandling;
using BotFramework.Sdk.UpdateHandling.Routes;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;

namespace ChatAdministration.Telegram.Presentation;

[CallbackPrefix("settings:")]
public sealed class SettingsCallbackTelegramHandler(
    ChatSettingsService service,
    IChatAdministrationStore store,
    IOptions<BotFrameworkOptions> botOptions) : IUpdateHandler
{
    private readonly BotFrameworkOptions options = botOptions.Value;

    public async Task HandleAsync(UpdateContext ctx)
    {
        var callback = ctx.Update.CallbackQuery;
        var message = callback?.Message;
        if (callback?.Data is null || message is null)
            return;

        var token = callback.Data["settings:".Length..];
        var state = await store.ConsumeSettingsCallbackAsync(
            token,
            new ChatId(message.Chat.Id),
            new UserId(callback.From.Id),
            ctx.Ct);
        if (state is null)
        {
            await store.EnqueueEffectAsync(
                new AnswerCallbackQueryEffect(callback.Id, "Кнопка устарела.", true),
                $"settings-callback-answer:{callback.Id}",
                EffectImportance.BestEffort,
                ctx.Ct);
            return;
        }

        var role = await TelegramRoleResolver.ResolveAsync(ctx.Bot, options, message.Chat.Id, callback.From.Id, ctx.Ct);
        await service.ExecuteAsync(
            new ChatSettingsCommand(
                $"settings-callback:{callback.Id}",
                $"settings-callback:{state.Token}",
                $"settings-callback:{state.Token}",
                state.ChatId,
                new UserId(callback.From.Id),
                state.Key,
                state.Value,
                message.MessageId,
                DateTimeOffset.UtcNow,
                role,
                DisplayName(callback.From.FirstName, callback.From.LastName, callback.From.Username, callback.From.Id)),
            ctx.Ct);
        await store.EnqueueEffectAsync(
            new AnswerCallbackQueryEffect(callback.Id, "Настройка обновлена."),
            $"settings-callback-answer:{callback.Id}",
            EffectImportance.BestEffort,
            ctx.Ct);
    }

    private static string DisplayName(string firstName, string? lastName, string? username, long id) =>
        string.Join(' ', new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value)))
        is { Length: > 0 } name
            ? name
            : username ?? $"User {id}";
}
