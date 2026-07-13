using BotFramework.Sdk.Execution;
using Games.PixelBattle.Application.Execution;
using Games.PixelBattle.Contracts;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class PixelBattleExecutionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(-1, "#FFFFFF", true, PixelUpdateStatus.InvalidIndex)]
    [InlineData(0, "invalid", true, PixelUpdateStatus.InvalidColor)]
    [InlineData(0, "#FFFFFF", false, PixelUpdateStatus.UnknownUser)]
    public void Decision_RejectsWithoutTileMutation(
        int index, string color, bool known, PixelUpdateStatus expected)
    {
        var state = new PixelBattleExecutionState(null, known, 0);
        var decision = new PixelBattleAction().Decide(Input(
            new(42, index, color, "command"), state));

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(expected, decision.Result.Status);
        Assert.Same(state, decision.NewState);
        Assert.Empty(decision.Events);
    }

    [Fact]
    public void Decision_ProducesStableVersionAndCommittedEvent()
    {
        var command = new PixelBattleCommand(42, 12, "#ffffff", "command");
        var state = new PixelBattleExecutionState(new(12, "#000000", 40, 7), true, 41);

        var first = new PixelBattleAction().Decide(Input(command, state));
        var second = new PixelBattleAction().Decide(Input(command, state));

        Assert.Equal(first.Result, second.Result);
        Assert.Equal(first.NewState, second.NewState);
        Assert.Equal(first.Events.Single(), second.Events.Single());
        Assert.Equal(DecisionStatus.Accepted, first.Status);
        Assert.Equal("00000000000000000041", first.Result.Update!.Versionstamp);
        Assert.Equal(41, first.NewState.Tile!.Version);
        Assert.IsType<PixelBattleTileUpdated>(Assert.Single(first.Events));
    }

    private static GameActionInput<PixelBattleExecutionState, PixelBattleCommand> Input(
        PixelBattleCommand command, PixelBattleExecutionState state) =>
        new(command, state, new WalletSnapshot(0), new Dictionary<string, QuotaSnapshot>(),
            EntropyValue.Empty, Now);
}
