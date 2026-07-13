using BotFramework.Sdk.Execution;
using Games.Redeem.Application.Execution;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class RedeemServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IssueDecision_CreatesCodeAndCommittedEvent()
    {
        var id = Guid.NewGuid();
        var command = new RedeemIssueCommand(id, 1, MiniGameIds.Dice, "issue");
        var decision = new RedeemIssueAction().Decide(Input(command, new(null)));

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(id, decision.Result);
        Assert.True(decision.NewState.Code!.Active);
        Assert.Single(decision.Events);
    }

    [Fact]
    public void CompleteDecision_ClaimsCodeAndGrantsCapacityBelowZero()
    {
        var code = Code(active: true);
        var command = new RedeemCompleteCommand(code.Code, 2, 20, code.FreeSpinGameId, "complete");
        var quotas = new Dictionary<string, QuotaSnapshot>
        {
            [RedeemCompleteAction.FreeSpinQuota] = new(0, 10),
        };
        var decision = new RedeemCompleteAction().Decide(Input(command, new(code), quotas));

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.False(decision.NewState.Code!.Active);
        Assert.Equal(QuotaEffectKind.Grant, Assert.Single(decision.Quotas).Kind);
        Assert.Single(decision.Events);
    }

    [Fact]
    public void CompleteDecision_InactiveCodeHasNoEffects()
    {
        var code = Code(active: false);
        var command = new RedeemCompleteCommand(code.Code, 2, 20, code.FreeSpinGameId, "complete");
        var decision = new RedeemCompleteAction().Decide(Input(command, new(code)));

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(RedeemError.AlreadyRedeemed, decision.Result.Error);
        Assert.Empty(decision.Quotas);
        Assert.Empty(decision.Events);
    }

    [Fact]
    public void CompleteDecision_UnlimitedGameClaimsWithoutQuotaMutation()
    {
        var code = Code(active: true);
        var command = new RedeemCompleteCommand(code.Code, 2, 20, code.FreeSpinGameId, "complete");
        var decision = new RedeemCompleteAction().Decide(Input(command, new(code)));
        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Empty(decision.Quotas);
    }

    private static RedeemCode Code(bool active) => new()
    {
        Code = Guid.Parse("33333333-3333-3333-3333-333333333333"), Active = active,
        IssuedBy = 1, IssuedAt = Now.ToUnixTimeMilliseconds(), FreeSpinGameId = MiniGameIds.Dice,
    };

    private static GameActionInput<RedeemExecutionState, TCommand> Input<TCommand>(
        TCommand command, RedeemExecutionState state,
        IReadOnlyDictionary<string, QuotaSnapshot>? quotas = null) =>
        new(command, state, new WalletSnapshot(0), quotas ?? new Dictionary<string, QuotaSnapshot>(),
            new EntropyValue([]), Now);
}
