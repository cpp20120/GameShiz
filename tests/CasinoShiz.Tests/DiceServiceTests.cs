using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Games.Dice.Application.Execution;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class DiceServiceTests
{
    [Fact]
    public async Task PlayAsync_ForwardedMessage_DoesNotEnterExecutor()
    {
        var executor = new RecordingExecutor(new DicePlayResult(DiceOutcome.Played));
        var service = new DiceService(executor, new FakeRuntimeTuning());

        var result = await service.PlayAsync(1, "u", 64, 100, 1, true, default);

        Assert.Equal(DiceOutcome.Forwarded, result.Outcome);
        Assert.Null(executor.Envelope);
    }

    [Fact]
    public async Task PlayAsync_MapsExistingArgumentsToAtomicCommand()
    {
        var expected = new DicePlayResult(DiceOutcome.Played, 77, 8, 169, 1, 2, 10);
        var executor = new RecordingExecutor(expected);
        var tuning = new FakeRuntimeTuning
        {
            Dice = new DiceOptions { Cost = 7, RedeemDropChance = 0.25 },
        };
        var service = new DiceService(executor, tuning);

        var result = await service.PlayAsync(42, "player", 64, -100, 99, false, default);

        Assert.Equal(expected, result);
        var command = Assert.IsType<DiceCommand>(executor.Envelope?.Command);
        Assert.Equal(42, command.UserId);
        Assert.Equal("player", command.DisplayName);
        Assert.Equal(64, command.DiceValue);
        Assert.Equal(-100, command.ChatId);
        Assert.Equal(99, command.SourceMessageId);
        Assert.Equal(7, command.Cost);
        Assert.Equal(0.25, command.RedeemDropChance);
        Assert.False(command.IsForwarded);
    }

    private sealed class RecordingExecutor(DicePlayResult result)
        : IAtomicGameExecutor<DiceCommand, NoGameState, DicePlayResult>
    {
        public Type StateType => typeof(NoGameState);

        public GameExecutionEnvelope<DiceCommand>? Envelope { get; private set; }

        public Task<DicePlayResult> ExecuteAsync(
            GameExecutionEnvelope<DiceCommand> envelope,
            CancellationToken ct)
        {
            Envelope = envelope;
            return Task.FromResult(result);
        }
    }
}
