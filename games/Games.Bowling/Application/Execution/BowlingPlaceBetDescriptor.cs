using BotFramework.Host.Execution;
using Microsoft.Extensions.Options;

namespace Games.Bowling.Application.Execution;

public sealed class BowlingPlaceBetDescriptor(IRuntimeTuningAccessor tuning, IOptions<BotFrameworkOptions> options)
    : BowlingExecutionDescriptor<BowlingPlaceBetCommand, BowlingBetResult>(tuning, options)
{
    public override string CommandId(BowlingPlaceBetCommand command) => command.CommandId;
    public override string AggregateId(BowlingPlaceBetCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(BowlingPlaceBetCommand command) => command.ChatId;
    public override string DisplayName(BowlingPlaceBetCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(BowlingPlaceBetCommand command) => new(command.UserId, command.ChatId);
    protected override long UserId(BowlingPlaceBetCommand command) => command.UserId;
}
