using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Games.DiceCube.Application.Execution;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace CasinoShiz.Tests;

[Collection("MiniGameSession")]
public sealed class DiceCubeServiceTests
{
    public DiceCubeServiceTests() => BotMiniGameSession.DangerousResetAllForTests();

    [Fact]
    public async Task PlaceBet_MapsStableTelegramCommandAndTuningToExecutor()
    {
        DiceCubePlaceBetCommand? captured = null;
        var service = CreateService(
            place: command =>
            {
                captured = command;
                return new CubeBetResult(CubeBetError.None, command.Amount, 75);
            });

        var result = await service.PlaceBetAsync(42, "alice", 84, 25, 123, CancellationToken.None);

        Assert.Equal(CubeBetError.None, result.Error);
        Assert.NotNull(captured);
        Assert.Equal("dicecube:bet:84:123:42", captured.CommandId);
        Assert.Equal(25, captured.Amount);
        Assert.Equal(10_000, captured.MaxBet);
        Assert.Equal((1, 2, 2), (captured.Mult4, captured.Mult5, captured.Mult6));
    }

    [Fact]
    public async Task Roll_MapsMessageIdAndReturnsExecutorResult()
    {
        DiceCubeRollCommand? captured = null;
        var expected = new CubeRollResult(CubeRollOutcome.Rolled, 6, 50, 2, 100, 150, 1, 10);
        var service = CreateService(roll: command =>
        {
            captured = command;
            return expected;
        });

        var result = await service.RollAsync(42, "alice", 84, 6, 456, CancellationToken.None);

        Assert.Equal(expected, result);
        Assert.NotNull(captured);
        Assert.Equal("dicecube:roll:84:456:42", captured.CommandId);
        Assert.Equal(6, captured.Face);
    }

    [Fact]
    public async Task Abort_MapsOriginalMessageIdToStableCompensationCommand()
    {
        DiceCubeAbortCommand? captured = null;
        var service = CreateService(abort: command =>
        {
            captured = command;
            return new DiceCubeAbortResult(true);
        });

        await service.AbortPendingBetAfterSendDiceFailedAsync(
            42, "alice", 84, 789, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("dicecube:abort:84:789:42", captured.CommandId);
        Assert.Equal("alice", captured.DisplayName);
    }

    [Fact]
    public async Task CompletedRollFeedsCooldownSnapshotIntoNextPlaceDecision()
    {
        DiceCubePlaceBetCommand? captured = null;
        var tuning = new FakeRuntimeTuning
        {
            DiceCube = new DiceCubeOptions { MinSecondsBetweenBets = 30 },
        };
        var service = CreateService(
            tuning,
            place: command =>
            {
                captured = command;
                return CubeBetResult.CooldownWait(100, command.CooldownSeconds);
            },
            roll: _ => new CubeRollResult(CubeRollOutcome.Rolled));

        await service.RollAsync(42, "alice", 84, 1, 1, CancellationToken.None);
        await service.PlaceBetAsync(42, "alice", 84, 10, 2, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.InRange(captured.CooldownSeconds, 1, 30);
    }

    private static DiceCubeService CreateService(
        FakeRuntimeTuning? tuning = null,
        Func<DiceCubePlaceBetCommand, CubeBetResult>? place = null,
        Func<DiceCubeRollCommand, CubeRollResult>? roll = null,
        Func<DiceCubeAbortCommand, DiceCubeAbortResult>? abort = null) =>
        new(
            new InMemoryDiceCubeBetStore(),
            new MemoryCache(new MemoryCacheOptions()),
            tuning ?? new FakeRuntimeTuning(),
            new NullMiniGameSessionGhostHeal(),
            new StubExecutor<DiceCubePlaceBetCommand, DiceCubePlaceBetState, CubeBetResult>(
                place ?? (_ => CubeBetResult.Fail(CubeBetError.InvalidAmount))),
            new StubExecutor<DiceCubeRollCommand, DiceCubePlaceBetState, CubeRollResult>(
                roll ?? (_ => new CubeRollResult(CubeRollOutcome.NoBet))),
            new StubExecutor<DiceCubeAbortCommand, DiceCubePlaceBetState, DiceCubeAbortResult>(
                abort ?? (_ => new DiceCubeAbortResult(false))));

    private sealed class StubExecutor<TCommand, TState, TResult>(Func<TCommand, TResult> execute)
        : IAtomicGameExecutor<TCommand, TState, TResult>
    {
        public Type StateType => typeof(TState);

        public Task<TResult> ExecuteAsync(GameExecutionEnvelope<TCommand> envelope, CancellationToken ct) =>
            Task.FromResult(execute(envelope.Command));
    }
}
