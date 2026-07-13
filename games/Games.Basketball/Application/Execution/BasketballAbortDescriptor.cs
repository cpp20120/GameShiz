using BotFramework.Host.Execution;
using Microsoft.Extensions.Options;

namespace Games.Basketball.Application.Execution;

public sealed class BasketballAbortDescriptor(
    IRuntimeTuningAccessor tuning,
    IOptions<BotFrameworkOptions> botOptions)
    : BasketballExecutionDescriptor<BasketballAbortCommand, BasketballAbortResult>(tuning, botOptions)
{
    public override string CommandId(BasketballAbortCommand command) => command.CommandId;
    public override string AggregateId(BasketballAbortCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(BasketballAbortCommand command) => command.ChatId;
    public override string DisplayName(BasketballAbortCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(BasketballAbortCommand command) => new(command.UserId, command.ChatId);
    protected override long UserId(BasketballAbortCommand command) => command.UserId;
}
