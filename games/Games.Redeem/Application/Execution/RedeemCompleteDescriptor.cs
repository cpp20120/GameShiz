using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Microsoft.Extensions.Options;

namespace Games.Redeem.Application.Execution;

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
