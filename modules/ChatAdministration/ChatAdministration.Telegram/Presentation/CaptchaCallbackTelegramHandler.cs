using ChatAdministration.Application.Services;
using BotFramework.Sdk.UpdateHandling;
using BotFramework.Sdk.UpdateHandling.Routes;
using Telegram.Bot.Types;

namespace ChatAdministration.Telegram.Presentation;

[CallbackPrefix("captcha:")]
public sealed class CaptchaCallbackTelegramHandler(VerificationService verification) : IUpdateHandler
{
    public async Task HandleAsync(UpdateContext ctx)
    {
        var callback = ctx.Update.CallbackQuery;
        if (callback?.Data is null || callback.Message is null)
            return;

        var parts = callback.Data.Split(':', 3, StringSplitOptions.None);
        if (parts.Length != 3 || !Guid.TryParseExact(parts[1], "N", out var sessionId))
            return;

        await verification.SubmitAsync(
            new ChatAdministration.Domain.Models.VerificationSessionId(sessionId),
            new ChatAdministration.Domain.Models.UserId(callback.From.Id),
            callback.Id,
            parts[2],
            callback.Message.MessageId,
            DateTimeOffset.UtcNow,
            ctx.Ct,
            new ChatAdministration.Domain.Models.ChatId(callback.Message.Chat.Id));
    }
}
