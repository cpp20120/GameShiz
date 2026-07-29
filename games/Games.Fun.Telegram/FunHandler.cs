using System.Globalization;
using System.Net;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Games.Fun.Telegram;

[Command("/roll")]
[Command("/choose")]
[Command("/ben")]
public sealed partial class FunHandler(
    FunService service,
    ILocalizer localizer,
    IOptions<FunOptions> options,
    ILogger<FunHandler> logger) : IUpdateHandler
{
    public async Task HandleAsync(UpdateContext ctx)
    {
        if (ctx.Update.Message?.Text is not { Length: > 0 } text)
            return;

        var command = CommandName(text);
        var arguments = Arguments(text);
        switch (command)
        {
            case "/roll":
                await HandleRollAsync(ctx, arguments);
                break;
            case "/choose":
                await HandleChooseAsync(ctx, arguments);
                break;
            case "/ben":
                await HandleBenAsync(ctx);
                break;
        }
    }

    private async Task HandleRollAsync(UpdateContext ctx, string arguments)
    {
        var outcome = service.Roll(string.IsNullOrWhiteSpace(arguments) ? null : arguments);
        var text = outcome.Question is null
            ? string.Format(CultureInfo.InvariantCulture, Loc("roll.percent"), outcome.Percentage)
            : string.Format(
                CultureInfo.InvariantCulture,
                Loc("roll.question"),
                WebUtility.HtmlEncode(outcome.Question),
                outcome.Percentage,
                outcome.FavorableCases,
                outcome.TotalCases,
                Loc($"roll.band.{outcome.Band.ToString().ToLowerInvariant()}"));

        await ctx.Bot.SendMessage(
            ctx.ChatId,
            text,
            parseMode: global::Telegram.Bot.Types.Enums.ParseMode.Html,
            replyParameters: new ReplyParameters { MessageId = ctx.Update.Message!.MessageId },
            cancellationToken: ctx.Ct);
    }

    private async Task HandleChooseAsync(UpdateContext ctx, string arguments)
    {
        var result = service.Choose(arguments);
        if (result.Error is { } error)
        {
            var key = error switch
            {
                ChoiceError.TooFew => "choose.too_few",
                ChoiceError.TooMany => "choose.too_many",
                ChoiceError.OptionTooLong => "choose.too_long",
                ChoiceError.EmptyOption => "choose.empty_option",
                _ => "choose.usage",
            };
            await ctx.Bot.SendMessage(
                ctx.ChatId,
                Loc(key),
                replyParameters: new ReplyParameters { MessageId = ctx.Update.Message!.MessageId },
                cancellationToken: ctx.Ct);
            return;
        }

        await ctx.Bot.SendMessage(
            ctx.ChatId,
            string.Format(CultureInfo.InvariantCulture, Loc("choose.result"), WebUtility.HtmlEncode(result.Selected!)),
            parseMode: global::Telegram.Bot.Types.Enums.ParseMode.Html,
            replyParameters: new ReplyParameters { MessageId = ctx.Update.Message!.MessageId },
            cancellationToken: ctx.Ct);
    }

    private async Task HandleBenAsync(UpdateContext ctx)
    {
        var choice = service.SelectBen();
        var sources = choice.Group == BenAnimationGroup.Primary
            ? options.Value.BenPrimary
            : options.Value.BenRare;
        if (choice.Index >= sources.Length || string.IsNullOrWhiteSpace(sources[choice.Index]))
        {
            await ctx.Bot.SendMessage(
                ctx.ChatId,
                Loc("ben.not_configured"),
                replyParameters: new ReplyParameters { MessageId = ctx.Update.Message!.MessageId },
                cancellationToken: ctx.Ct);
            return;
        }

        try
        {
            await SendAnimationAsync(ctx, sources[choice.Index]);
        }
        catch (OperationCanceledException) when (ctx.Ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogBenSendFailed(choice.Group, choice.Index, ex);
            await ctx.Bot.SendMessage(ctx.ChatId, Loc("ben.failed"), cancellationToken: ctx.Ct);
        }
    }

    private async Task SendAnimationAsync(UpdateContext ctx, string source)
    {
        var reply = new ReplyParameters { MessageId = ctx.Update.Message!.MessageId };
        if (source.StartsWith("file_id:", StringComparison.OrdinalIgnoreCase))
        {
            await ctx.Bot.SendAnimation(
                ctx.ChatId,
                InputFile.FromFileId(source["file_id:".Length..]),
                caption: Loc("ben.caption"),
                replyParameters: reply,
                cancellationToken: ctx.Ct);
            return;
        }

        if (Uri.TryCreate(source, UriKind.Absolute, out var uri)
            && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            await ctx.Bot.SendAnimation(
                ctx.ChatId,
                InputFile.FromUri(uri),
                caption: Loc("ben.caption"),
                replyParameters: reply,
                cancellationToken: ctx.Ct);
            return;
        }

        var path = Path.IsPathRooted(source)
            ? source
            : Path.Combine(AppContext.BaseDirectory, source);
        await using var stream = File.OpenRead(path);
        await ctx.Bot.SendAnimation(
            ctx.ChatId,
            InputFile.FromStream(stream, Path.GetFileName(path)),
            caption: Loc("ben.caption"),
            replyParameters: reply,
            cancellationToken: ctx.Ct);
    }

    private string Loc(string key) => localizer.Get("fun", key);

    private static string CommandName(string text)
    {
        var token = text.TrimStart().Split([' ', '\t', '\r', '\n'], 2)[0];
        var at = token.IndexOf('@');
        return (at >= 0 ? token[..at] : token).ToLowerInvariant();
    }

    private static string Arguments(string text)
    {
        var trimmed = text.TrimStart();
        var separator = trimmed.IndexOfAny([' ', '\t', '\r', '\n']);
        return separator < 0 ? string.Empty : trimmed[(separator + 1)..].Trim();
    }

    [LoggerMessage(EventId = 6801, Level = LogLevel.Warning, Message = "fun.ben.send_failed group={Group} index={Index}")]
    private partial void LogBenSendFailed(BenAnimationGroup group, int index, Exception exception);
}
