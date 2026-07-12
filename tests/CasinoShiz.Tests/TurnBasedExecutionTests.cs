using System.Globalization;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class TurnBasedExecutionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Decide_ValidTurnRunsGameRulesAndAdvancesRevision()
    {
        var decision = Decide(new MoveCommand(10, 4), State(revision: 4));

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(5, decision.NewState.Revision);
        Assert.Equal(1, decision.NewState.Moves);
        Assert.Equal("moved", decision.Result.Code);
    }

    [Theory]
    [InlineData(3, TurnGameStatus.Active, 10, 1, "stale_revision")]
    [InlineData(4, TurnGameStatus.Completed, 10, 1, "game_not_active")]
    [InlineData(4, TurnGameStatus.Active, 20, 1, "not_players_turn")]
    [InlineData(4, TurnGameStatus.Active, 10, -1, "turn_expired")]
    public void Decide_FrameworkRejectsInvalidTurnBeforeGameRules(
        long expectedRevision,
        TurnGameStatus status,
        long currentPlayer,
        int deadlineMinutes,
        string expectedReason)
    {
        var state = State(
            revision: 4,
            status: status,
            currentPlayer: currentPlayer,
            deadline: Now.AddMinutes(deadlineMinutes));

        var decision = Decide(new MoveCommand(10, expectedRevision), state);

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(expectedReason, decision.RejectionReason);
        Assert.Equal(expectedReason, decision.Result.Code);
        Assert.Equal(state, decision.NewState);
        Assert.Empty(decision.Economy);
        Assert.Empty(decision.Quotas);
        Assert.Empty(decision.Events);
    }

    [Fact]
    public void Decide_AcceptedGameRuleMustAdvanceExactlyOneRevision()
    {
        var action = new BrokenMoveAction();
        var input = Input(new MoveCommand(10, 4), State(revision: 4));

        var error = Assert.Throws<InvalidOperationException>(() => action.Decide(input));

        Assert.Contains("revision", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Registration_ProvidesActionDescriptorAndFrameworkStateStore()
    {
        var services = new ServiceCollection();
        services.AddAtomicTurnBasedGameAction<MoveCommand, TurnState, MoveAction, MoveResult, MoveDescriptor>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<MoveAction>(
            scope.ServiceProvider.GetRequiredService<IGameAction<MoveCommand, TurnState, MoveResult>>());
        Assert.IsType<MoveDescriptor>(
            scope.ServiceProvider.GetRequiredService<GameExecutionDescriptor<MoveCommand, TurnState, MoveResult>>());
        Assert.IsType<PostgresJsonGameStateStore<MoveCommand, TurnState, MoveResult>>(
            scope.ServiceProvider.GetRequiredService<IGameStateStore<MoveCommand, TurnState>>());
    }

    [Fact]
    public async Task ScheduledAtomicCommand_RoundTripsBackIntoExecutor()
    {
        var command = new MoveCommand(10, 4);
        var effect = ScheduleEffect.ScheduleCommand("turn-timeout", Now.AddMinutes(1), command);
        var executor = new CapturingExecutor();
        var scheduled = new AtomicGameScheduledCommand<MoveCommand, TurnState, MoveResult>(executor);

        await scheduled.ExecuteAsync(effect.Data!, CancellationToken.None);

        Assert.Equal(AtomicGameSchedule.JobKey<MoveCommand>(), effect.JobKey);
        Assert.Equal(command, executor.Command);
    }

    private static GameDecision<TurnState, MoveResult> Decide(MoveCommand command, TurnState state) =>
        new MoveAction().Decide(Input(command, state));

    private static GameActionInput<TurnState, MoveCommand> Input(MoveCommand command, TurnState state) =>
        new(command, state, new WalletSnapshot(100), new Dictionary<string, QuotaSnapshot>(), new EntropyValue([]), Now);

    private static TurnState State(
        long revision,
        TurnGameStatus status = TurnGameStatus.Active,
        long currentPlayer = 10,
        DateTimeOffset? deadline = null) =>
        new(revision, status, currentPlayer, deadline ?? Now.AddMinutes(1), 0);

    public sealed record MoveCommand(long PlayerId, long ExpectedRevision) : ITurnGameCommand<long>;

    public sealed record TurnState(
        long Revision,
        TurnGameStatus Status,
        long CurrentPlayerId,
        DateTimeOffset? TurnDeadline,
        int Moves) : ITurnBasedGameState<long>;

    public sealed record MoveResult(string Code);

    public sealed class MoveAction : TurnBasedGameAction<MoveCommand, TurnState, MoveResult, long>
    {
        protected override GameDecision<TurnState, MoveResult> DecideTurn(
            GameActionInput<TurnState, MoveCommand> input) =>
            new(
                DecisionStatus.Accepted,
                input.State with { Revision = input.State.Revision + 1, Moves = input.State.Moves + 1 },
                new MoveResult("moved"),
                [],
                [],
                [],
                [],
                []);

        protected override MoveResult CreateRejectedResult(TurnRejection<long> rejection) => new(rejection.Code);
    }

    private sealed class BrokenMoveAction : TurnBasedGameAction<MoveCommand, TurnState, MoveResult, long>
    {
        protected override GameDecision<TurnState, MoveResult> DecideTurn(
            GameActionInput<TurnState, MoveCommand> input) =>
            new(DecisionStatus.Accepted, input.State, new MoveResult("broken"), [], [], [], [], []);

        protected override MoveResult CreateRejectedResult(TurnRejection<long> rejection) => new(rejection.Code);
    }

    public sealed class MoveDescriptor : GameExecutionDescriptor<MoveCommand, TurnState, MoveResult>
    {
        public override string GameId => "test-turn";
        public override string CommandId(MoveCommand command) => $"test:{command.PlayerId}:{command.ExpectedRevision}";
        public override string AggregateId(MoveCommand command) => command.PlayerId.ToString(CultureInfo.InvariantCulture);
        public override long ChatId(MoveCommand command) => 1;
        public override string DisplayName(MoveCommand command) => "player";
        public override WalletIdentity Wallet(MoveCommand command) => new(command.PlayerId, 1);
        public override TurnState CreateInitialState(MoveCommand command) =>
            new(0, TurnGameStatus.Active, command.PlayerId, null, 0);
    }

    private sealed class CapturingExecutor : IAtomicGameExecutor<MoveCommand, TurnState, MoveResult>
    {
        public Type StateType => typeof(TurnState);

        public MoveCommand? Command { get; private set; }

        public Task<MoveResult> ExecuteAsync(GameExecutionEnvelope<MoveCommand> envelope, CancellationToken ct)
        {
            Command = envelope.Command;
            return Task.FromResult(new MoveResult("scheduled"));
        }
    }
}
