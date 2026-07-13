using System.Globalization;
using System.Net;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using BotFramework.Rendering;
using Games.Horse.Rendering;
using Microsoft.Extensions.Logging;

namespace Games.Challenges.Application.Handlers;

[Command("/challenge")]
[CallbackPrefix("ch:")]
public sealed partial class ChallengeHandler(
    IChallengeService service,
    ILocalizer localizer,
    IRenderQueue renders,
    IRenderHistory renderHistory,
    TimeProvider timeProvider,
    ILogger<ChallengeHandler> logger) : IUpdateHandler
{
    private static readonly TimeSpan HorseGifFrameDelay = TimeSpan.FromMilliseconds(60);
    private static readonly TimeSpan HorseResultDelayBuffer = TimeSpan.FromSeconds(1);

    public async Task HandleAsync(UpdateContext ctx)
    {
        if (ctx.Update.CallbackQuery is { } cbq)
        {
            await HandleCallbackAsync(ctx, cbq);
            return;
        }

        if (ctx.Update.Message is { Text: not null } msg)
            await HandleCommandAsync(ctx, msg);
    }

    private async Task HandleCommandAsync(UpdateContext ctx, Message msg)
    {
        var challenger = msg.From;
        if (challenger?.Id is null or 0)
            return;

        var chatId = msg.Chat.Id;
        var challengerName = DisplayName(challenger);
        var reply = new ReplyParameters { MessageId = msg.MessageId };
        var args = StripCommand(msg.Text!).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (!TryParseCommand(msg, args, out var target, out var username, out var amount, out var game))
        {
            await ctx.Bot.SendMessage(chatId, Loc("usage"),
                parseMode: ParseMode.Html, replyParameters: reply, cancellationToken: ctx.Ct);
            return;
        }

        if (target is null)
        {
            target = await service.FindKnownUserByUsernameAsync(chatId, username!, ctx.Ct);
            if (target is null)
            {
                await ctx.Bot.SendMessage(chatId, string.Format(System.Globalization.CultureInfo.InvariantCulture, Loc("target.not_found"), Html(username!)),
                    parseMode: ParseMode.Html, replyParameters: reply, cancellationToken: ctx.Ct);
                return;
            }
        }

        var result = await service.CreateAsync(
            challenger.Id,
            challengerName,
            target,
            chatId,
            amount,
            game,
            ctx.Ct);

        if (result.Error != ChallengeCreateError.None)
        {
            await ctx.Bot.SendMessage(chatId, CreateErrorText(result),
                parseMode: ParseMode.Html, replyParameters: reply, cancellationToken: ctx.Ct);
            return;
        }

        var challenge = result.Challenge!;
        await ctx.Bot.SendMessage(
            chatId,
            string.Format(System.Globalization.CultureInfo.InvariantCulture, Loc("created"),
                Mention(challenge.ChallengerId, challenge.ChallengerName),
                Mention(challenge.TargetId, challenge.TargetName),
                challenge.Amount,
                ChallengeGameCatalog.DisplayName(challenge.Game),
                ChallengeGameCatalog.Emoji(challenge.Game)),
            parseMode: ParseMode.Html,
            replyParameters: reply,
            replyMarkup: BuildMarkup(challenge.Id),
            cancellationToken: ctx.Ct);
    }

    private async Task HandleCallbackAsync(UpdateContext ctx, CallbackQuery cbq)
    {
        var (action, challengeId) = ParseCallback(cbq.Data);
        if (challengeId == Guid.Empty || action is not ("a" or "d"))
        {
            await AnswerCallbackAsync(ctx, cbq);
            return;
        }

        if (string.Equals(action, "d", StringComparison.Ordinal))
        {
            var decline = await service.DeclineAsync(challengeId, cbq.From.Id, ctx.Ct);
            await AnswerCallbackAsync(ctx, cbq, CallbackErrorText(decline), decline != ChallengeAcceptError.None);
            if (decline == ChallengeAcceptError.None && cbq.Message is not null)
            {
                await ctx.Bot.EditMessageText(cbq.Message.Chat.Id, cbq.Message.MessageId, Loc("declined"),
                    cancellationToken: ctx.Ct);
            }

            return;
        }

        var begun = await service.BeginAcceptAsync(challengeId, cbq.From.Id, ctx.Ct);
        if (begun.Error != ChallengeAcceptError.None)
        {
            await AnswerCallbackAsync(ctx, cbq, CallbackErrorText(begun.Error), alert: true);
            return;
        }

        await AnswerCallbackAsync(ctx, cbq, Loc("accepted.toast"));
        var challenge = begun.Challenge!;
        if (cbq.Message is not null)
        {
            await ctx.Bot.EditMessageText(
                cbq.Message.Chat.Id,
                cbq.Message.MessageId,
                string.Format(System.Globalization.CultureInfo.InvariantCulture, Loc("accepted"),
                    Mention(challenge.ChallengerId, challenge.ChallengerName),
                    Mention(challenge.TargetId, challenge.TargetName),
                    ChallengeGameCatalog.Emoji(challenge.Game)),
                parseMode: ParseMode.Html,
                cancellationToken: ctx.Ct);
        }

        if (challenge.Game == ChallengeGame.Horse)
            await RaceAndSettleAsync(ctx, challenge, cbq.Message?.MessageId);
        else if (challenge.Game == ChallengeGame.Blackjack)
            await BlackjackAndSettleAsync(ctx, challenge);
        else
            await RollAndSettleAsync(ctx, challenge, cbq.Message?.MessageId);
    }

    private async Task RollAndSettleAsync(UpdateContext ctx, Challenge challenge, int? replyToMessageId)
    {
        var emoji = ChallengeGameCatalog.Emoji(challenge.Game);
        var reply = replyToMessageId is null ? null : new ReplyParameters { MessageId = replyToMessageId.Value };

        try
        {
            var challengerDice = await ctx.Bot.SendDice(
                challenge.ChatId,
                emoji: emoji,
                replyParameters: reply,
                cancellationToken: ctx.Ct);
            var targetDice = await ctx.Bot.SendDice(
                challenge.ChatId,
                emoji: emoji,
                replyParameters: reply,
                cancellationToken: ctx.Ct);

            if (challengerDice.Dice is not { Value: > 0 } c || targetDice.Dice is not { Value: > 0 } t)
            {
                await service.FailAcceptedAsync(challenge, ctx.Ct);
                await ctx.Bot.SendMessage(challenge.ChatId, Loc("roll.failed"), cancellationToken: ctx.Ct);
                return;
            }

            await Task.Delay(4000, ctx.Ct);
            var result = await service.CompleteAcceptedAsync(challenge, c.Value, t.Value, ctx.Ct);
            await ctx.Bot.SendMessage(challenge.ChatId, ResultText(result),
                parseMode: ParseMode.Html, cancellationToken: ctx.Ct);
        }
        catch
        {
            await service.FailAcceptedAsync(challenge, CancellationToken.None);
            await ctx.Bot.SendMessage(challenge.ChatId, Loc("roll.failed"), cancellationToken: CancellationToken.None);
        }
    }

    private async Task RaceAndSettleAsync(UpdateContext ctx, Challenge challenge, int? replyToMessageId)
    {
        var reply = replyToMessageId is null ? null : new ReplyParameters { MessageId = replyToMessageId.Value };
        var winner = SpeedGenerator.GenPlaces(2);
        var challengerScore = winner == 0 ? 2 : 1;
        var targetScore = winner == 1 ? 2 : 1;
        ChallengeAcceptResult result;

        try
        {
            // Settle the atomic game command before optional CPU/network media work.
            result = await service.CompleteAcceptedAsync(challenge, challengerScore, targetScore, ctx.Ct);
        }
        catch
        {
            await service.FailAcceptedAsync(challenge, CancellationToken.None);
            await ctx.Bot.SendMessage(challenge.ChatId, Loc("roll.failed"), cancellationToken: CancellationToken.None);
            return;
        }

        try
        {
            const int variants = 3;
            var variant = challenge.Id.ToByteArray()[0] % variants;
            var spec = new HorseRaceRenderSpec(2, winner, variant);
            var artifact = await renders.GetOrRenderAsync(spec, RenderPriority.Interactive, ctx.Ct);
            var frameCount = HorseRaceRenderJob.EstimateFrameCount(spec);
            await renderHistory.RecordAsync(new RenderHistoryEntry(
                "challenges",
                challenge.ChatId.ToString(CultureInfo.InvariantCulture),
                challenge.Id.ToString("N"),
                artifact.Key,
                timeProvider.GetUtcNow(),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["game"] = "horse",
                    ["winner"] = winner.ToString(CultureInfo.InvariantCulture),
                    ["challenger"] = challenge.ChallengerId.ToString(CultureInfo.InvariantCulture),
                    ["target"] = challenge.TargetId.ToString(CultureInfo.InvariantCulture),
                }), ctx.Ct);

            await using var stream = new MemoryStream(artifact.Content);
            await ctx.Bot.SendAnimation(
                challenge.ChatId,
                InputFile.FromStream(stream, "challenge-horse.gif"),
                caption: string.Format(System.Globalization.CultureInfo.InvariantCulture, Loc("horse.caption"),
                    Mention(challenge.ChallengerId, challenge.ChallengerName),
                    Mention(challenge.TargetId, challenge.TargetName)),
                parseMode: ParseMode.Html,
                replyParameters: reply,
                cancellationToken: ctx.Ct);

            await Task.Delay((HorseGifFrameDelay * frameCount) + HorseResultDelayBuffer, ctx.Ct);
        }
        catch (OperationCanceledException) when (ctx.Ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            LogHorseMediaFailed(challenge.Id, ex);
        }

        await ctx.Bot.SendMessage(challenge.ChatId, HorseResultText(result),
            parseMode: ParseMode.Html, cancellationToken: ctx.Ct);
    }

    private async Task BlackjackAndSettleAsync(UpdateContext ctx, Challenge challenge)
    {
        try
        {
            var duel = PlayBlackjackDuel();
            var challengerScore = BlackjackScore(duel.ChallengerTotal, duel.ChallengerNatural);
            var targetScore = BlackjackScore(duel.TargetTotal, duel.TargetNatural);
            var result = await service.CompleteAcceptedAsync(challenge, challengerScore, targetScore, ctx.Ct);
            await ctx.Bot.SendMessage(challenge.ChatId, BlackjackResultText(result, duel),
                parseMode: ParseMode.Html, cancellationToken: ctx.Ct);
        }
        catch
        {
            await service.FailAcceptedAsync(challenge, CancellationToken.None);
            await ctx.Bot.SendMessage(challenge.ChatId, Loc("roll.failed"), cancellationToken: CancellationToken.None);
        }
    }

    private bool TryParseCommand(
        Message msg,
        string[] args,
        out ChallengeUser? target,
        out string? username,
        out int amount,
        out ChallengeGame game)
    {
        target = null;
        username = null;
        amount = 0;
        game = default;

        var offset = 0;
        if (args.Length > 0 && args[0].StartsWith('@'))
        {
            username = args[0][1..].Trim();
            offset = 1;
        }
        else if (msg.ReplyToMessage?.From is { } replied && replied.Id != 0)
        {
            target = new ChallengeUser(replied.Id, DisplayName(replied));
        }

        return (target is not null || !string.IsNullOrWhiteSpace(username))
            && args.Length >= offset + 2
            && int.TryParse(args[offset], System.Globalization.CultureInfo.InvariantCulture, out amount) && ChallengeGameCatalog.TryParse(args[offset + 1], out game);
    }

    private static string StripCommand(string text)
    {
        var parts = text.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? parts[1] : "";
    }

    private static (string Action, Guid ChallengeId) ParseCallback(string? data)
    {
        var parts = data?.Split(':');
        if (parts is not { Length: 3 } || !string.Equals(parts[0], "ch", StringComparison.Ordinal))
            return ("", Guid.Empty);

        return Guid.TryParse(parts[2], out var id) ? (parts[1], id) : ("", Guid.Empty);
    }

    private static InlineKeyboardMarkup BuildMarkup(Guid challengeId) => new([
        [
            InlineKeyboardButton.WithCallbackData("✅ Accept", $"ch:a:{challengeId:N}"),
            InlineKeyboardButton.WithCallbackData("❌ Decline", $"ch:d:{challengeId:N}"),
        ],
    ]);

    private string CreateErrorText(ChallengeCreateResult result) => result.Error switch
    {
        ChallengeCreateError.InvalidAmount => Loc("err.amount"),
        ChallengeCreateError.SelfChallenge => Loc("err.self"),
        ChallengeCreateError.NotEnoughCoins => string.Format(System.Globalization.CultureInfo.InvariantCulture, Loc("err.not_enough"), result.Balance),
        ChallengeCreateError.AlreadyPending => Loc("err.pending"),
        _ => Loc("usage"),
    };

    private string CallbackErrorText(ChallengeAcceptError error) => error switch
    {
        ChallengeAcceptError.None => "",
        ChallengeAcceptError.NotTarget => Loc("err.not_target"),
        ChallengeAcceptError.Expired => Loc("err.expired"),
        ChallengeAcceptError.ChallengerNotEnoughCoins => Loc("err.challenger_poor"),
        ChallengeAcceptError.TargetNotEnoughCoins => Loc("err.target_poor"),
        ChallengeAcceptError.AlreadyResolved => Loc("err.resolved"),
        _ => Loc("err.not_found"),
    };

    private string ResultText(ChallengeAcceptResult result)
    {
        var challenge = result.Challenge!;
        if (result.IsTie)
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, Loc("result.tie"), Challenge.ChallengerRollLabel(result.ChallengerRoll),
                challenge.TargetRollLabel(result.TargetRoll),
                challenge.Amount);
        }

        return string.Format(System.Globalization.CultureInfo.InvariantCulture, Loc("result.win"),
            Mention(challenge.ChallengerId, challenge.ChallengerName),
            result.ChallengerRoll,
            Mention(challenge.TargetId, challenge.TargetName),
            result.TargetRoll,
            Mention(result.WinnerId, result.WinnerName),
            result.Payout,
            result.Fee);
    }

    private string HorseResultText(ChallengeAcceptResult result)
    {
        var challenge = result.Challenge!;
        var challengerHorse = result.ChallengerRoll > result.TargetRoll ? "🏆 #1" : "#1";
        var targetHorse = result.TargetRoll > result.ChallengerRoll ? "🏆 #2" : "#2";

        return string.Format(System.Globalization.CultureInfo.InvariantCulture, Loc("horse.result"),
            Mention(challenge.ChallengerId, challenge.ChallengerName),
            challengerHorse,
            Mention(challenge.TargetId, challenge.TargetName),
            targetHorse,
            Mention(result.WinnerId, result.WinnerName),
            result.Payout,
            result.Fee);
    }

    private string BlackjackResultText(ChallengeAcceptResult result, BlackjackDuel duel)
    {
        var challenge = result.Challenge!;
        var challengerLine = FormatBlackjackLine(duel.ChallengerCards, duel.ChallengerTotal, duel.ChallengerNatural);
        var targetLine = FormatBlackjackLine(duel.TargetCards, duel.TargetTotal, duel.TargetNatural);

        if (result.IsTie)
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, Loc("blackjack.tie"),
                Mention(challenge.ChallengerId, challenge.ChallengerName),
                challengerLine,
                Mention(challenge.TargetId, challenge.TargetName),
                targetLine,
                challenge.Amount);
        }

        return string.Format(System.Globalization.CultureInfo.InvariantCulture, Loc("blackjack.win"),
            Mention(challenge.ChallengerId, challenge.ChallengerName),
            challengerLine,
            Mention(challenge.TargetId, challenge.TargetName),
            targetLine,
            Mention(result.WinnerId, result.WinnerName),
            result.Payout,
            result.Fee);
    }

    private static BlackjackDuel PlayBlackjackDuel()
    {
        var deck = Deck.BuildShuffled();
        var challenger = AutoPlayBlackjackHand(ref deck);
        var target = AutoPlayBlackjackHand(ref deck);
        return new BlackjackDuel(
            challenger.Cards,
            target.Cards,
            challenger.Total,
            target.Total,
            challenger.Natural,
            target.Natural);
    }

    private static (string[] Cards, int Total, bool Natural) AutoPlayBlackjackHand(ref string deck)
    {
        var cards = Deck.Draw(ref deck, 2).ToList();
        while (BlackjackHandValue.Compute(cards) < 17)
            cards.Add(Deck.Draw(ref deck, 1)[0]);

        return ([.. cards], BlackjackHandValue.Compute(cards), BlackjackHandValue.IsNaturalBlackjack(cards));
    }

    private int BlackjackScore(int total, bool natural) =>
        total > 21 ? 0 : natural ? 22 : total;

    private string FormatBlackjackLine(string[] cards, int total, bool natural)
    {
        var suffix = total > 21 ? " bust" : natural ? " blackjack" : "";
        return string.Create(CultureInfo.InvariantCulture, $"{Html(string.Join(' ', cards.Select(FormatCard)))} = <b>{total}</b>{suffix}");
    }

    private static string FormatCard(string card)
    {
        if (card.Length < 2)
            return card;

        var rank = string.Equals(card[..^1], "T", StringComparison.Ordinal) ? "10" : card[..^1];
        var suit = card[^1] switch
        {
            'S' => "♠",
            'H' => "♥",
            'D' => "♦",
            'C' => "♣",
            _ => card[^1].ToString(),
        };
        return rank + suit;
    }

    private string DisplayName(User user) =>
        user.Username ?? user.FirstName ?? string.Create(CultureInfo.InvariantCulture, $"User ID: {user.Id}");

    private string Mention(long userId, string displayName) =>
        $"<a href=\"tg://user?id={userId}\">{Html(displayName)}</a>";

    private string Html(string value) => WebUtility.HtmlEncode(value);

    private string Loc(string key) => localizer.Get("challenges", key);

    private static Task AnswerCallbackAsync(
        UpdateContext ctx,
        CallbackQuery cbq,
        string? text = null,
        bool alert = false) =>
        ctx.Bot.AnswerCallbackQuery(cbq.Id, text, showAlert: alert, cancellationToken: ctx.Ct);

    [LoggerMessage(EventId = 7210, Level = LogLevel.Warning, Message = "challenge horse media failed challenge_id={ChallengeId}; settlement remains committed")]
    private partial void LogHorseMediaFailed(Guid challengeId, Exception exception);

    private sealed record BlackjackDuel(
        string[] ChallengerCards,
        string[] TargetCards,
        int ChallengerTotal,
        int TargetTotal,
        bool ChallengerNatural,
        bool TargetNatural);
}
