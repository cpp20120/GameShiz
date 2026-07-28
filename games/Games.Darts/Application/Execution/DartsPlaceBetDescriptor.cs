using BotFramework.Host.Execution;
using Microsoft.Extensions.Options;

namespace Games.Darts.Application.Execution;

public sealed class DartsPlaceBetDescriptor(IRuntimeTuningAccessor tuning, IOptions<BotFrameworkOptions> botOptions)
    : DartsQueuedDescriptor<DartsPlaceBetCommand, DartsBetResult>(tuning, botOptions)
{
    public override string CommandId(DartsPlaceBetCommand command) => command.CommandId;
    public override string AggregateId(DartsPlaceBetCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(DartsPlaceBetCommand command) => command.ChatId;
    public override string DisplayName(DartsPlaceBetCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(DartsPlaceBetCommand command) => new(command.UserId, command.ChatId);
    protected override long UserId(DartsPlaceBetCommand command) => command.UserId;
}
