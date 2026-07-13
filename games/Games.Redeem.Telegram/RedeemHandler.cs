using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Games.Redeem.Application.Handlers;

[Command("/redeem")]
[Command("/codegen")]
[CallbackPrefix("rd:")]
public sealed partial class RedeemHandler(
    IRedeemClient service,
    ILocalizer localizer,
    IOptions<RedeemOptions> options,
    IOptions<BotFrameworkOptions> botOptions,
    IHostApplicationLifetime lifetime,
    RedeemCaptchaTimeouts timeouts,
    ILogger<RedeemHandler> logger) : IUpdateHandler
{
    private readonly RedeemOptions _opts = options.Value;
    private readonly BotFrameworkOptions _botOpts = botOptions.Value;

    public async Task HandleAsync(UpdateContext ctx)
    {
        if (ctx.Update.CallbackQuery is { } cbq)
        {
            await HandleCallbackAsync(ctx, cbq);
            return;
        }

        var msg = ctx.Update.Message;
        if (msg?.Text == null) return;

        if (msg.Text.StartsWith("/codegen", StringComparison.OrdinalIgnoreCase))
            await HandleCodeGenAsync(ctx, msg);
        else if (msg.Text.StartsWith("/redeem", StringComparison.OrdinalIgnoreCase))
            await HandleRedeemAsync(ctx, msg);
    }

    private async Task HandleCodeGenAsync(UpdateContext ctx, Message msg)
    {
        var userId = msg.From?.Id ?? 0;
        if (userId == 0 || !IsCodegenAdmin(userId)) return;

        var count = ParseCodegenCount(msg.Text);
        var lines = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var code = await service.IssueAdminCodeAsync(userId, freeSpinGameId: null, ctx.Ct);
            lines.Add($"/redeem {code}");
        }

        await ctx.Bot.SendMessage(msg.Chat.Id, string.Join('\n', lines), cancellationToken: ctx.Ct);
    }

    private async Task HandleRedeemAsync(UpdateContext ctx, Message msg)
    {
        var userId = msg.From?.Id ?? 0;
        if (userId == 0) return;
        var chatId = msg.Chat.Id;
        var reply = new ReplyParameters { MessageId = msg.MessageId };

        if (msg.Chat.Type != ChatType.Private)
        {
            await ctx.Bot.SendMessage(chatId, Loc("err.only_private"),
                replyParameters: reply, cancellationToken: ctx.Ct);
            return;
        }

        var parts = msg.Text!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var codeText = parts.Length > 1 ? parts[1] : "";
        var displayName = msg.From?.Username ?? msg.From?.FirstName ?? string.Create(CultureInfo.InvariantCulture, $"User ID: {userId}");

        var result = await service.BeginAsync(userId, chatId, displayName, codeText, ctx.Ct);

        switch (result.Error)
        {
            case RedeemClientError.InvalidCode:
                await ctx.Bot.SendMessage(chatId, Loc("err.invalid_code"), cancellationToken: ctx.Ct);
                return;
            case RedeemClientError.AlreadyRedeemed:
                await ctx.Bot.SendMessage(chatId, Loc("err.already_redeemed"), cancellationToken: ctx.Ct);
                return;
            case RedeemClientError.SelfRedeem:
                await ctx.Bot.SendMessage(chatId, Loc("err.self_redeem"), cancellationToken: ctx.Ct);
                return;
            case RedeemClientError.NoUser:
                await ctx.Bot.SendMessage(chatId, Loc("err.no_user"), cancellationToken: ctx.Ct);
                return;
        }

        var captcha = result.Captcha!;
        var markup = BuildCaptchaMarkup(result.CodeGuid, captcha);

        var captchaMsg = await ctx.Bot.SendMessage(chatId,
            string.Format(CultureInfo.InvariantCulture, Loc("captcha.prompt"), captcha.Pattern),
            parseMode: ParseMode.Html,
            replyMarkup: markup,
            cancellationToken: ctx.Ct);

        var cts = timeouts.Schedule(result.CodeGuid, lifetime.ApplicationStopping);
        _ = ScheduleTimeoutAsync(ctx.Bot, chatId, captchaMsg.MessageId, result.CodeGuid, cts);
    }

    private async Task HandleCallbackAsync(UpdateContext ctx, CallbackQuery cbq)
    {
        try { await ctx.Bot.AnswerCallbackQuery(cbq.Id, cancellationToken: ctx.Ct); }
        catch (ApiRequestException ex) { LogCallbackAnswerFailed(cbq.Id, ex); }

        var userId = cbq.From.Id;
        var chatId = cbq.Message?.Chat.Id ?? 0;
        if (chatId == 0 || cbq.Data == null) return;

        var parts = cbq.Data.Split(':');
        if (parts.Length != 3 || !string.Equals(parts[0], "rd", StringComparison.Ordinal)) return;
        if (!Guid.TryParse(parts[1], out var codeGuid)) return;
        if (!int.TryParse(parts[2], System.Globalization.CultureInfo.InvariantCulture, out var chosenId)) return;

        // Cancel the pending timeout the moment the user picks an answer —
        // otherwise the fire-and-forget Task.Delay still lands and tells the
        // user they "took too long" even on a successful redeem.
        timeouts.TryCancel(codeGuid);

        var passed = await service.VerifyCaptchaAsync(userId, codeGuid, chosenId, ctx.Ct);

        try
        {
            if (cbq.Message != null)
                await ctx.Bot.DeleteMessage(chatId, cbq.Message.MessageId, cancellationToken: ctx.Ct);
        }
        catch (ApiRequestException ex) { LogCaptchaDeleteFailed(chatId, cbq.Message?.MessageId ?? 0, ex); }

        if (!passed)
        {
            await ctx.Bot.SendMessage(chatId, Loc("captcha.wrong"), cancellationToken: ctx.Ct);
            return;
        }

        var result = await service.CompleteAsync(userId, chatId, codeGuid, ctx.Ct);
        if (result.Error == RedeemClientError.AlreadyRedeemed)
        {
            await ctx.Bot.SendMessage(chatId, Loc("err.lost_race"), cancellationToken: ctx.Ct);
            return;
        }

        await ctx.Bot.SendMessage(chatId, Loc("redeem.success"), cancellationToken: ctx.Ct);
    }

    private int ParseCodegenCount(string? text)
    {
        var parts = text?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (parts.Length <= 1 || !int.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var requested))
            return 1;

        return Math.Clamp(requested, 1, Math.Max(1, _opts.MaxCodegenCount));
    }

    private bool IsCodegenAdmin(long userId) =>
        _opts.Admins.Contains(userId) || _botOpts.Admins.Contains(userId);

    private async Task ScheduleTimeoutAsync(
        ITelegramBotClient bot, long chatId, int messageId, Guid codeGuid, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(_opts.CaptchaTimeoutMs, cts.Token);
            // Atomically claim the slot. If a callback is racing us and removed
            // the entry first, Forget returns false and we silently bow out so
            // the user doesn't see a stray "took too long" after answering.
            if (!timeouts.Forget(codeGuid, cts)) return;
            try { await bot.DeleteMessage(chatId, messageId); }
            catch (ApiRequestException ex) { LogCaptchaDeleteFailed(chatId, messageId, ex); }
            try { await bot.SendMessage(chatId, Loc("captcha.timeout")); }
            catch (ApiRequestException ex) { LogTimeoutMessageFailed(chatId, ex); }
        }
        catch (OperationCanceledException)
        {
            // Cancelled either by the callback handler (user answered) or by
            // app shutdown. Either way: nothing to send, nothing to clean.
        }
        catch (Exception ex)
        {
            LogTimeoutFailed(ex);
            timeouts.Forget(codeGuid, cts);
        }
        finally
        {
            cts.Dispose();
        }
    }

    private static InlineKeyboardMarkup BuildCaptchaMarkup(Guid codeGuid, RedeemCaptchaChallenge captcha)
    {
        var rows = (int)Math.Ceiling(captcha.Items.Count / 5.0);
        var splitAfter = Math.Max(1, (int)Math.Ceiling(captcha.Items.Count / (double)rows));

        var keyboardRows = new List<InlineKeyboardButton[]>();
        for (var i = 0; i < rows; i++)
        {
            keyboardRows.Add(captcha.Items
                .Skip(i * splitAfter).Take(splitAfter)
                .Select(item => InlineKeyboardButton.WithCallbackData(item.Text, string.Create(CultureInfo.InvariantCulture, $"rd:{codeGuid}:{item.Data}")))
                .ToArray());
        }
        return new InlineKeyboardMarkup(keyboardRows);
    }

    private string Loc(string key) => localizer.Get("redeem", key);

    [LoggerMessage(LogLevel.Warning, "redeem.captcha_timeout_failed")]
    partial void LogTimeoutFailed(Exception ex);

    [LoggerMessage(LogLevel.Debug, "redeem.callback.answer_failed id={CallbackQueryId}")]
    partial void LogCallbackAnswerFailed(string callbackQueryId, Exception ex);

    [LoggerMessage(LogLevel.Debug, "redeem.captcha.delete_failed chat={ChatId} message={MessageId}")]
    partial void LogCaptchaDeleteFailed(long chatId, int messageId, Exception ex);

    [LoggerMessage(LogLevel.Debug, "redeem.captcha.timeout_message_failed chat={ChatId}")]
    partial void LogTimeoutMessageFailed(long chatId, Exception ex);
}
