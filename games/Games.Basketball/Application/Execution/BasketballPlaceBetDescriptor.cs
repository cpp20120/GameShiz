using BotFramework.Host.Execution;
using Microsoft.Extensions.Options;

namespace Games.Basketball.Application.Execution;

public sealed class BasketballPlaceBetDescriptor(
    IRuntimeTuningAccessor tuning,
    IOptions<BotFrameworkOptions> botOptions)
    : BasketballExecutionDescriptor<BasketballPlaceBetCommand, BasketballBetResult>(tuning, botOptions)
{
    public override string CommandId(BasketballPlaceBetCommand command) => command.CommandId;
    public override string AggregateId(BasketballPlaceBetCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(BasketballPlaceBetCommand command) => command.ChatId;
    public override string DisplayName(BasketballPlaceBetCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(BasketballPlaceBetCommand command) => new(command.UserId, command.ChatId);
    protected override long UserId(BasketballPlaceBetCommand command) => command.UserId;
}
