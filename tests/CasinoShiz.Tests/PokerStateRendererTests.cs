using Xunit;

namespace CasinoShiz.Tests;

public sealed class PokerStateRendererTests
{
    private static readonly ILocalizer Loc = new PokerLocalizer();

    [Theory]
    [InlineData("", "??")]
    [InlineData("X", "??")]
    [InlineData("TS", "10♠")]
    [InlineData("AH", "A♥")]
    [InlineData("7D", "7♦")]
    [InlineData("2C", "2♣")]
    [InlineData("9Z", "9?")]
    public void RenderCard_MapsRanksAndSuits(string card, string expected)
    {
        Assert.Equal(expected, PokerStateRenderer.RenderCard(card));
    }

    [Theory]
    [InlineData("", 0, "—")]
    [InlineData("", 2, "🂠 🂠")]
    [InlineData("TS AH", 0, "10♠ A♥")]
    [InlineData("TS", 3, "10♠ 🂠 🂠")]
    public void RenderCards_ParsesCardsAndPadsWhenRequested(string cards, int padToCount, string expected)
    {
        Assert.Equal(expected, PokerStateRenderer.RenderCards(cards, padToCount));
    }

    [Fact]
    public void RenderTable_SeatingEscapesNamesAndShowsWaitingState()
    {
        var table = Table(PokerTableStatus.Seating, PokerPhase.None);
        var seats = new List<PokerSeat>
        {
            Seat(1, 10, "<Alice>", 900),
            Seat(0, 11, "Bob", 1_100),
        };

        var rendered = PokerStateRenderer.RenderTable(table, seats, viewerUserId: null, Loc);

        Assert.Contains("Table ROOM", rendered, StringComparison.Ordinal);
        Assert.Contains("seating", rendered, StringComparison.Ordinal);
        Assert.Contains("Community: —", rendered, StringComparison.Ordinal);
        Assert.Contains("Pot 0 Bet 0", rendered, StringComparison.Ordinal);
        Assert.Contains("waiting", rendered, StringComparison.Ordinal);
        Assert.Contains("&lt;Alice&gt;", rendered, StringComparison.Ordinal);
        Assert.True(rendered.IndexOf("Bob", StringComparison.Ordinal) < rendered.IndexOf("&lt;Alice&gt;", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderTable_ActiveViewerTurnShowsHoleCardsAndToCall()
    {
        var table = Table(PokerTableStatus.HandActive, PokerPhase.Turn);
        table.CommunityCards = "AS KD QH JC 9S";
        table.ButtonSeat = 0;
        table.CurrentSeat = 1;
        table.CurrentBet = 120;
        table.Pot = 500;
        var seats = new List<PokerSeat>
        {
            Seat(0, 10, "Alice", 900, currentBet: 120),
            Seat(1, 11, "Bob", 700, currentBet: 40, holeCards: "TS 9H"),
            Seat(2, 12, "Cleo", 0, PokerSeatStatus.AllIn, currentBet: 120),
            Seat(3, 13, "Dora", 500, PokerSeatStatus.Folded),
            Seat(4, 14, "Eve", 500, PokerSeatStatus.SittingOut),
        };

        var rendered = PokerStateRenderer.RenderTable(table, seats, viewerUserId: 11, Loc);

        Assert.Contains("turn", rendered, StringComparison.Ordinal);
        Assert.Contains("Community: A♠ K♦ Q♥ J♣", rendered, StringComparison.Ordinal);
        Assert.Contains("🔘 Alice — 900 · Bet 120", rendered, StringComparison.Ordinal);
        Assert.Contains("➡️ Bob (you) — 700 · Bet 40", rendered, StringComparison.Ordinal);
        Assert.Contains("all-in", rendered, StringComparison.Ordinal);
        Assert.Contains("folded", rendered, StringComparison.Ordinal);
        Assert.Contains("sitting out", rendered, StringComparison.Ordinal);
        Assert.Contains("Your cards: 10♠ 9♥", rendered, StringComparison.Ordinal);
        Assert.Contains("To call 80", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderTable_ActiveViewerCanCheckWhenCurrentBetIsMatched()
    {
        var table = Table(PokerTableStatus.HandActive, PokerPhase.River);
        table.CommunityCards = "AS KD QH JC 9S";
        table.CurrentSeat = 0;
        table.CurrentBet = 100;
        var seats = new List<PokerSeat>
        {
            Seat(0, 10, "Alice", 900, currentBet: 100, holeCards: "2C 3D"),
        };

        var rendered = PokerStateRenderer.RenderTable(table, seats, viewerUserId: 10, Loc);

        Assert.Contains("Community: A♠ K♦ Q♥ J♣ 9♠", rendered, StringComparison.Ordinal);
        Assert.Contains("Can check", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderTable_NonCurrentViewerSeesCardsWithoutActionPrompt()
    {
        var table = Table(PokerTableStatus.HandActive, PokerPhase.Flop);
        table.CommunityCards = "AS KD QH";
        table.CurrentSeat = 1;
        var seats = new List<PokerSeat>
        {
            Seat(0, 10, "Alice", 900, holeCards: "2C 3D"),
            Seat(1, 11, "Bob", 900),
        };

        var rendered = PokerStateRenderer.RenderTable(table, seats, viewerUserId: 10, Loc);

        Assert.Contains("Community: A♠ K♦ Q♥", rendered, StringComparison.Ordinal);
        Assert.Contains("Your cards: 2♣ 3♦", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("Can check", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("To call", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderShowdown_EscapesNamesShowsRanksCardsAndWinnings()
    {
        var table = Table(PokerTableStatus.HandComplete, PokerPhase.Showdown);
        table.CommunityCards = "AS KS QS JS TS";
        var results = new[]
        {
            (Seat: Seat(0, 10, "<Winner>", 1_500), Rank: (HandRank?)new HandRank(HandCategory.StraightFlush, [14]), Won: 900, HoleCards: "AH KH"),
            (Seat: Seat(1, 11, "Loser", 0), Rank: (HandRank?)null, Won: 0, HoleCards: ""),
        };

        var rendered = PokerStateRenderer.RenderShowdown(table, results, Loc);

        Assert.Contains("Showdown", rendered, StringComparison.Ordinal);
        Assert.Contains("Community: A♠ K♠ Q♠ J♠ 10♠", rendered, StringComparison.Ordinal);
        Assert.Contains("&lt;Winner&gt; — <b>A♥ K♥</b>", rendered, StringComparison.Ordinal);
        Assert.Contains("Стрит-флеш", rendered, StringComparison.Ordinal);
        Assert.Contains("<b>+900</b>", rendered, StringComparison.Ordinal);
        Assert.Contains("Loser — <b>—</b>", rendered, StringComparison.Ordinal);
    }

    private static PokerTable Table(PokerTableStatus status, PokerPhase phase) => new()
    {
        InviteCode = "ROOM",
        Status = status,
        Phase = phase,
    };

    private static PokerSeat Seat(
        int position,
        long userId,
        string name,
        int stack,
        PokerSeatStatus status = PokerSeatStatus.Seated,
        int currentBet = 0,
        string holeCards = "") => new()
        {
            InviteCode = "ROOM",
            Position = position,
            UserId = userId,
            DisplayName = name,
            Stack = stack,
            Status = status,
            CurrentBet = currentBet,
            HoleCards = holeCards,
        };

    private sealed class PokerLocalizer : ILocalizer
    {
        private static readonly Dictionary<string, string> Values = new(StringComparer.Ordinal)
        {
            ["state.header"] = "Table {0}",
            ["phase.seating"] = "seating",
            ["phase.preflop"] = "preflop",
            ["phase.flop"] = "flop",
            ["phase.turn"] = "turn",
            ["phase.river"] = "river",
            ["phase.showdown"] = "showdown",
            ["state.community"] = "Community: {0}",
            ["state.pot_bet"] = "Pot {0} Bet {1}",
            ["state.waiting_for_start"] = "waiting",
            ["seat.folded"] = "folded",
            ["seat.allin"] = "all-in",
            ["seat.sitting_out"] = "sitting out",
            ["seat.bet"] = "Bet {0}",
            ["seat.you"] = "you",
            ["state.your_cards"] = "Your cards: {0}",
            ["state.to_call"] = "To call {0}",
            ["state.can_check"] = "Can check",
            ["showdown.header"] = "Showdown",
        };

        public string Get(string moduleId, string key, string cultureCode = "ru") =>
            Values.GetValueOrDefault(key, key);

        public string GetPlural(string moduleId, string key, int count, string cultureCode = "ru") => key;
    }
}
