using BotFramework.Host.Execution;
using Microsoft.Extensions.Options;

namespace Games.Darts.Application.Execution;

public sealed class DartsResolveRoundDescriptor(IRuntimeTuningAccessor tuning, IOptions<BotFrameworkOptions> botOptions)
    : DartsQueuedDescriptor<DartsResolveRoundCommand, DartsThrowResult>(tuning, botOptions)
{
    public override IReadOnlyList<string> EntropyNames => [DartsResolveRoundAction.RedeemDropEntropy];
    public override string CommandId(DartsResolveRoundCommand command) => command.CommandId;
    public override string AggregateId(DartsResolveRoundCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(DartsResolveRoundCommand command) => command.ChatId;
    public override string DisplayName(DartsResolveRoundCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(DartsResolveRoundCommand command) => new(command.UserId, command.ChatId);
    protected override long UserId(DartsResolveRoundCommand command) => command.UserId;
}
