using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Microsoft.Extensions.Options;

namespace Games.Redeem.Application.Execution;

public sealed class RedeemCompleteAction
    : IGameAction<RedeemCompleteCommand, RedeemExecutionState, CompleteRedeemResult>
{
    public const string FreeSpinQuota = "redeem.free-spin";

    public GameDecision<RedeemExecutionState, CompleteRedeemResult> Decide(
        GameActionInput<RedeemExecutionState, RedeemCompleteCommand> input)
    {
        if (input.State.Code is not { Active: true } code)
        {
            return new(DecisionStatus.Rejected, input.State,
                new(RedeemError.AlreadyRedeemed), [], [], [], [], [], "already_redeemed");
        }
        if (!string.Equals(code.FreeSpinGameId, input.Command.ExpectedGameId, StringComparison.Ordinal))
            throw new InvalidOperationException("Redeem code game changed while executing.");
        var now = input.UtcNow.ToUnixTimeMilliseconds();
        var redeemed = new RedeemCode
        {
            Code = code.Code, Active = false, IssuedBy = code.IssuedBy, IssuedAt = code.IssuedAt,
            FreeSpinGameId = code.FreeSpinGameId, RedeemedBy = input.Command.UserId, RedeemedAt = now,
        };
        var quotaEffects = input.Quotas.ContainsKey(FreeSpinQuota)
            ? new[] { QuotaEffect.Grant(FreeSpinQuota) }
            : [];
        return new(DecisionStatus.Accepted, new(redeemed),
            new(RedeemError.None, code.FreeSpinGameId), [], quotaEffects, [],
            [new RedeemCodeRedeemed(code.Code, code.IssuedBy, input.Command.UserId, code.FreeSpinGameId, now)], []);
    }
}
