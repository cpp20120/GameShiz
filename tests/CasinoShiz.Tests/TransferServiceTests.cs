using BotFramework.Sdk.Execution;
using Games.Transfer.Application.Execution;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class TransferServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Decide_InsufficientFundsHasNoEffects()
    {
        var decision = Decide(balance: 50, recipientBalance: 20, net: 100, fee: 3);

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(TransferError.InsufficientFunds, decision.Result.Error);
        Assert.Empty(decision.Economy);
        Assert.Null(decision.CustomEffects);
    }

    [Fact]
    public void Decide_EmitsAtomicSenderDebitRecipientCreditAndCommittedEvent()
    {
        var decision = Decide(balance: 500, recipientBalance: 40, net: 100, fee: 3);

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(397, decision.Result.SenderBalance);
        Assert.Equal(140, decision.Result.RecipientBalance);
        Assert.Equal(103, Assert.Single(decision.Economy).Amount);
        var credit = Assert.IsType<WalletEconomyEffect>(Assert.Single(decision.CustomEffects!));
        Assert.Equal((2L, 10L, 100L), (credit.UserId, credit.BalanceScopeId, credit.Amount));
        Assert.Single(decision.Events);
    }

    [Theory]
    [InlineData(100, 3, 103)]
    [InlineData(1, 1, 2)]
    [InlineData(200, 6, 206)]
    [InlineData(33, 1, 34)]
    [InlineData(42, 2, 44)]
    public void Fee_RoundsPercentToHalfThenWhole_WithMinOne(int net, int expectedFee, int expectedTotal)
    {
        Assert.Equal(expectedFee, TransferOptions.ComputeFeeCoins(net, 0.03, 1));
        Assert.Equal(expectedTotal, TransferOptions.ComputeTotalDebit(net, 0.03, 1));
    }

    private static GameDecision<TransferState, TransferAttemptResult> Decide(
        int balance, int recipientBalance, int net, int fee)
    {
        var command = new TransferCommand(1, 2, 10, "a", "b", net, fee, net + fee, "transfer-1");
        return new TransferAction().Decide(new(command, new(recipientBalance), new(balance),
            new Dictionary<string, QuotaSnapshot>(), new EntropyValue([]), Now));
    }
}
