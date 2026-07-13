using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Microsoft.Extensions.Options;

namespace Games.Redeem.Application.Execution;

public sealed record RedeemExecutionState(RedeemCode? Code);

public sealed record RedeemIssueCommand(
    Guid Code,
    long IssuedBy,
    string FreeSpinGameId,
    string CommandId);

public sealed record RedeemCompleteCommand(
    Guid Code,
    long UserId,
    long BalanceScopeId,
    string ExpectedGameId,
    string CommandId);

public sealed class RedeemIssueAction
    : IGameAction<RedeemIssueCommand, RedeemExecutionState, Guid>
{
    public GameDecision<RedeemExecutionState, Guid> Decide(
        GameActionInput<RedeemExecutionState, RedeemIssueCommand> input)
    {
        if (input.State.Code is not null)
            return new(DecisionStatus.Rejected, input.State, Guid.Empty, [], [], [], [], [], "code_exists");
        var code = new RedeemCode
        {
            Code = input.Command.Code, Active = true, IssuedBy = input.Command.IssuedBy,
            IssuedAt = input.UtcNow.ToUnixTimeMilliseconds(), FreeSpinGameId = input.Command.FreeSpinGameId,
        };
        return new(DecisionStatus.Accepted, new(code), code.Code, [], [], [],
            [new RedeemCodeIssued(code.Code, code.IssuedBy, code.FreeSpinGameId, code.IssuedAt)], []);
    }
}

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

public sealed class RedeemIssueDescriptor
    : GameExecutionDescriptor<RedeemIssueCommand, RedeemExecutionState, Guid>
{
    public override string GameId => "redeem";
    public override bool UsesPrimaryWallet => false;
    public override string CommandId(RedeemIssueCommand command) => command.CommandId;
    public override string AggregateId(RedeemIssueCommand command) => command.Code.ToString("N");
    public override long ChatId(RedeemIssueCommand command) => 0;
    public override string DisplayName(RedeemIssueCommand command) => "redeem issuer";
    public override WalletIdentity Wallet(RedeemIssueCommand command) => new(command.IssuedBy, 0);
}

public sealed class RedeemCompleteDescriptor(
    IRuntimeTuningAccessor tuning,
    IOptions<BotFrameworkOptions> botOptions)
    : GameExecutionDescriptor<RedeemCompleteCommand, RedeemExecutionState, CompleteRedeemResult>
{
    public override string GameId => "redeem";
    public override bool UsesPrimaryWallet => false;
    public override string CommandId(RedeemCompleteCommand command) => command.CommandId;
    public override string AggregateId(RedeemCompleteCommand command) => command.Code.ToString("N");
    public override long ChatId(RedeemCompleteCommand command) => command.BalanceScopeId;
    public override string DisplayName(RedeemCompleteCommand command) => $"User ID: {command.UserId}";
    public override WalletIdentity Wallet(RedeemCompleteCommand command) =>
        new(command.UserId, command.BalanceScopeId);

    public override IReadOnlyList<QuotaIdentity> Quotas(RedeemCompleteCommand command, DateTimeOffset utcNow)
    {
        var options = tuning.TelegramDiceDailyLimit;
        var unlimitedAdmin = command.UserId == command.BalanceScopeId
            && botOptions.Value.Admins.Contains(command.UserId);
        var limit = unlimitedAdmin ? 0 : options.GetMaxRollsPerUserPerDay(command.ExpectedGameId);
        if (limit <= 0) return [];
        var date = DateOnly.FromDateTime(utcNow.AddHours(options.TimezoneOffsetHours).DateTime);
        return [new(RedeemCompleteAction.FreeSpinQuota, command.ExpectedGameId,
            command.UserId, command.BalanceScopeId, date, limit)];
    }
}
