using BotFramework.Host.Execution;
using Microsoft.Extensions.Options;

namespace Games.Bowling.Application.Execution;

public sealed class BowlingAbortDescriptor(IRuntimeTuningAccessor tuning, IOptions<BotFrameworkOptions> options)
    : BowlingExecutionDescriptor<BowlingAbortCommand, BowlingAbortResult>(tuning, options)
{
    public override string CommandId(BowlingAbortCommand command) => command.CommandId;
    public override string AggregateId(BowlingAbortCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(BowlingAbortCommand command) => command.ChatId;
    public override string DisplayName(BowlingAbortCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(BowlingAbortCommand command) => new(command.UserId, command.ChatId);
    protected override long UserId(BowlingAbortCommand command) => command.UserId;
}
