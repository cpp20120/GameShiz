using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Microsoft.Extensions.Options;

namespace Games.Redeem.Application.Execution;

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
