using BotFramework.Host.Execution;
using Microsoft.Extensions.Options;

namespace Games.Bowling.Application.Execution;

public sealed class BowlingRollDescriptor(IRuntimeTuningAccessor tuning, IOptions<BotFrameworkOptions> options)
    : BowlingExecutionDescriptor<BowlingRollCommand, BowlingRollResult>(tuning, options)
{
    public override IReadOnlyList<string> EntropyNames => [BowlingRollAction.RedeemDropEntropy];
    public override string CommandId(BowlingRollCommand command) => command.CommandId;
    public override string AggregateId(BowlingRollCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(BowlingRollCommand command) => command.ChatId;
    public override string DisplayName(BowlingRollCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(BowlingRollCommand command) => new(command.UserId, command.ChatId);
    protected override long UserId(BowlingRollCommand command) => command.UserId;
}
