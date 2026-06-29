using Xunit;

namespace CasinoShiz.Tests;

public sealed class PokerBoardRendererTests
{
    private static readonly ILocalizer Localizer = new BoardLocalizer();

    [Theory]
    [InlineData(PokerPhase.None, "seating")]
    [InlineData(PokerPhase.PreFlop, "preflop")]
    [InlineData(PokerPhase.Flop, "flop")]
    [InlineData(PokerPhase.Turn, "turn")]
    [InlineData(PokerPhase.River, "river")]
    [InlineData(PokerPhase.Showdown, "showdown")]
    public void Caption_ContainsLocalizedPhaseAndEconomy(PokerPhase phase, string expectedPhase)
    {
        var table = Table(phase);

        var caption = PokerBoardRenderer.Caption(table, Localizer);

        Assert.Contains("Table ROOM", caption, StringComparison.Ordinal);
        Assert.Contains(expectedPhase, caption, StringComparison.Ordinal);
        Assert.Contains("Банк: <b>250</b>", caption, StringComparison.Ordinal);
        Assert.Contains("Ставка: <b>50</b>", caption, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(PokerPhase.None)]
    [InlineData(PokerPhase.Flop)]
    [InlineData(PokerPhase.Turn)]
    [InlineData(PokerPhase.River)]
    public void Render_ProducesPngForBoardPhasesAndSeatStatuses(PokerPhase phase)
    {
        var table = Table(phase);
        var seats = new List<PokerSeat>
        {
            Seat(0, "", PokerSeatStatus.Seated, 0),
            Seat(1, "A very long player display name", PokerSeatStatus.Folded, 20),
            Seat(2, "AllIn", PokerSeatStatus.AllIn, 50),
            Seat(3, "Out", PokerSeatStatus.SittingOut, 0),
        };

        var bytes = PokerBoardRenderer.Render(new TableSnapshot(table, seats), Localizer);

        Assert.True(bytes.Length > 1_000);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes[..8]);
    }

    private static PokerTable Table(PokerPhase phase) => new()
    {
        InviteCode = "ROOM",
        Status = phase == PokerPhase.None ? PokerTableStatus.Seating : PokerTableStatus.HandActive,
        Phase = phase,
        Pot = 250,
        CurrentBet = 50,
        CommunityCards = "AS KH QD JC TS",
        ButtonSeat = 0,
        CurrentSeat = 1,
    };

    private static PokerSeat Seat(int position, string name, PokerSeatStatus status, int currentBet) => new()
    {
        InviteCode = "ROOM",
        Position = position,
        UserId = position + 1,
        DisplayName = name,
        Stack = 500,
        Status = status,
        CurrentBet = currentBet,
    };

    private sealed class BoardLocalizer : ILocalizer
    {
        public string Get(string moduleId, string key, string cultureCode = "ru") => key switch
        {
            "state.header" => "Table {0}",
            "phase.preflop" => "preflop",
            "phase.flop" => "flop",
            "phase.turn" => "turn",
            "phase.river" => "river",
            "phase.showdown" => "showdown",
            _ => "seating",
        };

        public string GetPlural(string moduleId, string key, int count, string cultureCode = "ru") =>
            Get(moduleId, key, cultureCode);
    }
}
