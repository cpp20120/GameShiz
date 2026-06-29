using Xunit;

namespace CasinoShiz.Tests;

public sealed class BlackjackRendererTests
{
    private static readonly ILocalizer Localizer = new BlackjackTestLocalizer();

    [Fact]
    public void Render_ActiveHand_HidesDealerHoleCardAndFormatsCards()
    {
        var snapshot = Snapshot(
            playerCards: ["AS", "TD"],
            dealerCards: ["KH", "7C"],
            playerTotal: 21,
            dealerTotal: 17,
            dealerHoleRevealed: false);

        var rendered = BlackjackRenderer.Render(snapshot, Localizer);

        Assert.Contains("Dealer: K♥ 🂠", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("7♣", rendered, StringComparison.Ordinal);
        Assert.Contains("Player: A♠ 10♦ (21)", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("Balance:", rendered, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(BlackjackOutcome.PlayerBlackjack, "Blackjack +20")]
    [InlineData(BlackjackOutcome.PlayerWin, "Player win +20")]
    [InlineData(BlackjackOutcome.DealerBust, "Dealer bust +20")]
    [InlineData(BlackjackOutcome.PlayerBust, "Player bust +20")]
    [InlineData(BlackjackOutcome.DealerWin, "Dealer win +20")]
    [InlineData(BlackjackOutcome.Push, "Push +20")]
    public void Render_CompletedHand_ShowsOutcomeAndBalance(
        BlackjackOutcome outcome,
        string expectedOutcome)
    {
        var snapshot = Snapshot(
            dealerHoleRevealed: true,
            outcome: outcome,
            bet: 10,
            payout: 30,
            playerCoins: 120);

        var rendered = BlackjackRenderer.Render(snapshot, Localizer);

        Assert.Contains("Dealer: A♠ K♥ (21)", rendered, StringComparison.Ordinal);
        Assert.Contains(expectedOutcome, rendered, StringComparison.Ordinal);
        Assert.Contains("Balance: 120", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildKeyboard_ActiveHandWithoutDouble_HasHitAndStand()
    {
        var keyboard = BlackjackRenderer.BuildKeyboard(Snapshot(canDouble: false), Localizer);

        var callbacks = Assert.IsType<Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup>(keyboard)
            .InlineKeyboard.SelectMany(row => row).Select(button => button.CallbackData!).ToArray();
        Assert.Equal(["bj:hit", "bj:stand"], callbacks);
    }

    [Fact]
    public void BuildKeyboard_ActiveHandWithDouble_AddsSecondRow()
    {
        var keyboard = Assert.IsType<Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup>(
            BlackjackRenderer.BuildKeyboard(Snapshot(canDouble: true), Localizer));

        Assert.Equal(2, keyboard.InlineKeyboard.Count());
        Assert.Equal("bj:double", keyboard.InlineKeyboard.Last().Single().CallbackData);
    }

    [Fact]
    public void BuildKeyboard_CompletedHand_ReturnsNull()
    {
        var keyboard = BlackjackRenderer.BuildKeyboard(
            Snapshot(outcome: BlackjackOutcome.Push),
            Localizer);

        Assert.Null(keyboard);
    }

    private static BlackjackSnapshot Snapshot(
        string[]? playerCards = null,
        string[]? dealerCards = null,
        int playerTotal = 18,
        int dealerTotal = 21,
        int bet = 10,
        int playerCoins = 100,
        bool dealerHoleRevealed = false,
        bool canDouble = false,
        BlackjackOutcome? outcome = null,
        int payout = 30) => new(
            playerCards ?? ["QH", "8D"],
            dealerCards ?? ["AS", "KH"],
            playerTotal,
            dealerTotal,
            bet,
            playerCoins,
            dealerHoleRevealed,
            canDouble,
            outcome,
            payout);

    private sealed class BlackjackTestLocalizer : ILocalizer
    {
        private static readonly Dictionary<string, string> Values =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["render.header"] = "Bet: {0}",
                ["render.dealer"] = "Dealer: {0}{1}",
                ["render.player"] = "Player: {0} ({1})",
                ["render.balance"] = "Balance: {0}",
                ["outcome.player_blackjack"] = "Blackjack +{0}",
                ["outcome.player_win"] = "Player win +{0}",
                ["outcome.dealer_bust"] = "Dealer bust +{0}",
                ["outcome.player_bust"] = "Player bust +{0}",
                ["outcome.dealer_win"] = "Dealer win +{0}",
                ["outcome.push"] = "Push +{0}",
                ["btn.hit"] = "Hit",
                ["btn.stand"] = "Stand",
                ["btn.double"] = "Double",
            };

        public string Get(string moduleId, string key, string cultureCode = "ru") => Values[key];

        public string GetPlural(string moduleId, string key, int count, string cultureCode = "ru") =>
            Get(moduleId, key, cultureCode);
    }
}
