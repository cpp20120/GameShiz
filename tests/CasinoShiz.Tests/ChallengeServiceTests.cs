using BotFramework.Sdk.Execution;
using Games.Challenges.Application.Execution;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class ChallengeServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(1, 10, ChallengeCreateError.SelfChallenge)]
    [InlineData(2, 9, ChallengeCreateError.InvalidAmount)]
    [InlineData(2, 1001, ChallengeCreateError.InvalidAmount)]
    public void CreateDecision_RejectsInvalidRequest(long targetId, int amount, ChallengeCreateError error)
    {
        var decision = new ChallengeCreateAction().Decide(CreateInput(targetId, amount, balance: 100));
        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(error, decision.Result.Error);
    }

    [Fact]
    public void CreateDecision_CreatesPendingAggregateWithoutDebitingStake()
    {
        var decision = new ChallengeCreateAction().Decide(CreateInput(2, 25, balance: 100));
        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(ChallengeStatus.Pending, decision.NewState.Challenge!.Status);
        Assert.Empty(decision.Economy);
        Assert.Single(decision.Events);
    }

    [Fact]
    public void AcceptDecision_EmitsTwoAtomicWalletDebits()
    {
        var challenge = Challenge(status: ChallengeStatus.Pending);
        var command = new ChallengeAcceptCommand(challenge.Id, 2, "bob", 10, "accept",
            [new(1, 10), new(2, 10)]);
        var decision = new ChallengeAcceptAction().Decide(Input(command, new(challenge, false, 100, 100)));

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(ChallengeStatus.Accepted, decision.NewState.Challenge!.Status);
        Assert.Equal(2, decision.CustomEffects!.Count);
        Assert.All(decision.CustomEffects, effect => Assert.Equal(25, Assert.IsType<WalletEconomyEffect>(effect).Amount));
    }

    [Fact]
    public void AcceptDecision_TargetWithoutCoinsFailsWithoutPartialDebitOrRefund()
    {
        var challenge = Challenge(status: ChallengeStatus.Pending);
        var command = new ChallengeAcceptCommand(challenge.Id, 2, "bob", 10, "accept",
            [new(1, 10), new(2, 10)]);
        var decision = new ChallengeAcceptAction().Decide(Input(command, new(challenge, false, 100, 0)));

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(ChallengeAcceptError.TargetNotEnoughCoins, decision.Result.Error);
        Assert.Equal(ChallengeStatus.Failed, decision.NewState.Challenge!.Status);
        Assert.Null(decision.CustomEffects);
    }

    [Fact]
    public void CompleteDecision_PaysWinnerMinusFee()
    {
        var challenge = Challenge(status: ChallengeStatus.Accepted, amount: 100);
        var command = new ChallengeCompleteCommand(challenge.Id, 1, "alice", 10, 6, 3, 250,
            "complete", [new(1, 10), new(2, 10)]);
        var decision = new ChallengeCompleteAction().Decide(Input(command, new(challenge, false, 0, 0)));

        Assert.Equal(195, decision.Result.Payout);
        Assert.Equal(5, decision.Result.Fee);
        Assert.Equal(195, Assert.IsType<WalletEconomyEffect>(Assert.Single(decision.CustomEffects!)).Amount);
    }

    [Fact]
    public void CompleteDecision_TieRefundsBothStakes()
    {
        var challenge = Challenge(status: ChallengeStatus.Accepted, amount: 40);
        var command = new ChallengeCompleteCommand(challenge.Id, 1, "alice", 10, 4, 4, 250,
            "complete", [new(1, 10), new(2, 10)]);
        var decision = new ChallengeCompleteAction().Decide(Input(command, new(challenge, false, 0, 0)));

        Assert.True(decision.Result.IsTie);
        Assert.Equal(2, decision.CustomEffects!.Count);
    }

    private static GameActionInput<ChallengeExecutionState, ChallengeCreateCommand> CreateInput(
        long targetId, int amount, int balance) =>
        Input(new ChallengeCreateCommand(Guid.Parse("11111111-1111-1111-1111-111111111111"),
            1, "alice", new(targetId, "bob"), 10, amount, ChallengeGame.Dice,
            10, 1000, TimeSpan.FromMinutes(10), "create", [new(1, 10)]),
            new(null, false, 0, 0), balance);

    private static Challenge Challenge(ChallengeStatus status, int amount = 25) =>
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), 10, 1, "alice", 2, "bob",
            amount, ChallengeGame.Dice, status, Now, Now.AddMinutes(10));

    private static GameActionInput<ChallengeExecutionState, TCommand> Input<TCommand>(
        TCommand command, ChallengeExecutionState state, int balance = 0) =>
        new(command, state, new WalletSnapshot(balance), new Dictionary<string, QuotaSnapshot>(),
            new EntropyValue([]), Now);
}
