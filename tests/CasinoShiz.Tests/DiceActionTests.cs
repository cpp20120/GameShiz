using BotFramework.Sdk.Execution;
using Games.Dice.Application.Execution;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class DiceActionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000);

    [Theory]
    [MemberData(nameof(AllTelegramSlotValues))]
    public void Decide_AcceptsEveryTelegramSlotValue(int diceValue)
    {
        var decision = Decide(diceValue: diceValue);

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(DiceOutcome.Played, decision.Result.Outcome);
    }

    [Theory]
    [InlineData(1, 21)]
    [InlineData(22, 23)]
    [InlineData(43, 30)]
    [InlineData(64, 77)]
    [InlineData(16, 17)]
    [InlineData(11, 11)]
    [InlineData(6, 7)]
    [InlineData(37, 1)]
    public void Decide_UsesExistingPayoutTable(int diceValue, int expectedPrize)
    {
        var decision = Decide(diceValue: diceValue);

        Assert.Equal(expectedPrize, decision.Result.Prize);
    }

    [Fact]
    public void Decide_RejectsInsufficientBalanceWithoutEffects()
    {
        var decision = Decide(balance: 0);

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(DiceOutcome.NotEnoughCoins, decision.Result.Outcome);
        Assert.Equal("insufficient_balance", decision.RejectionReason);
        Assert.Empty(decision.Economy);
        Assert.Empty(decision.Quotas);
        Assert.IsType<DiceRollRejected>(Assert.Single(decision.Events));
    }

    [Fact]
    public void Decide_RejectsExhaustedQuotaWithoutEffects()
    {
        var decision = Decide(quotaUsed: 10, quotaLimit: 10);

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(DiceOutcome.DailyRollLimitExceeded, decision.Result.Outcome);
        Assert.Equal("daily_roll_limit", decision.RejectionReason);
        Assert.Empty(decision.Economy);
        Assert.Empty(decision.Quotas);
        Assert.IsType<DiceRollRejected>(Assert.Single(decision.Events));
    }

    [Fact]
    public void Decide_ZeroQuotaLimitMeansUnlimited()
    {
        var decision = Decide(quotaLimit: 0);

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(0, decision.Result.DailyDiceUsed);
        Assert.Empty(decision.Quotas);
    }

    [Fact]
    public void Decide_SameInputProducesEqualDecision()
    {
        var first = Decide(diceValue: 64, entropy: 0.01, redeemDropChance: 0.02);
        var second = Decide(diceValue: 64, entropy: 0.01, redeemDropChance: 0.02);

        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.NewState, second.NewState);
        Assert.Equal(first.Result, second.Result);
        Assert.Equal(first.Economy, second.Economy);
        Assert.Equal(first.Quotas, second.Quotas);
        Assert.Equal(first.Records, second.Records);
        Assert.Equal(first.Events, second.Events);
        Assert.Equal(first.Schedules, second.Schedules);
        Assert.Equal(first.RejectionReason, second.RejectionReason);
    }

    [Fact]
    public void Decide_ContainsOnlySdkEffectsAndDomainEvents()
    {
        var decision = Decide();

        Assert.All(decision.Economy, effect => Assert.Equal("BotFramework.Sdk", effect.GetType().Assembly.GetName().Name));
        Assert.All(decision.Quotas, effect => Assert.Equal("BotFramework.Sdk", effect.GetType().Assembly.GetName().Name));
        Assert.All(decision.Records, record => Assert.IsAssignableFrom<IGameRecord>(record));
        Assert.All(decision.Events, domainEvent => Assert.IsAssignableFrom<IDomainEvent>(domainEvent));
    }

    [Fact]
    public void Decide_ProducesTransactionNeutralHistoryRecord()
    {
        var decision = Decide(diceValue: 64);

        var record = Assert.IsType<DiceRollRecord>(Assert.Single(decision.Records));
        Assert.Equal(64, record.DiceValue);
        Assert.Equal(77, record.Prize);
        Assert.Equal(Now, record.RolledAt);
    }

    public static TheoryData<int> AllTelegramSlotValues => new(Enumerable.Range(1, 64).ToArray());

    private static GameDecision<NoGameState, DicePlayResult> Decide(
        int diceValue = 64,
        long balance = 100,
        long quotaUsed = 0,
        long quotaLimit = 10,
        double entropy = 0.5,
        double redeemDropChance = 0) =>
        new DiceAction().Decide(new GameActionInput<NoGameState, DiceCommand>(
            new DiceCommand(1, "player", diceValue, 100, diceValue, false, 7, redeemDropChance),
            default,
            new WalletSnapshot(balance),
            new Dictionary<string, QuotaSnapshot>(StringComparer.Ordinal)
            {
                [DiceAction.DailyRollQuota] = new(quotaUsed, quotaLimit),
            },
            new EntropyValue([KeyValuePair.Create(DiceAction.RedeemDropEntropy, entropy)]),
            Now));
}
