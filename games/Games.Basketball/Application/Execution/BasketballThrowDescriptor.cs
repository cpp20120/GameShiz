using BotFramework.Host.Execution;
using Microsoft.Extensions.Options;

namespace Games.Basketball.Application.Execution;

public sealed class BasketballThrowDescriptor(
    IRuntimeTuningAccessor tuning,
    IOptions<BotFrameworkOptions> botOptions)
    : BasketballExecutionDescriptor<BasketballThrowCommand, BasketballThrowResult>(tuning, botOptions)
{
    public override IReadOnlyList<string> EntropyNames => [BasketballThrowAction.RedeemDropEntropy];
    public override string CommandId(BasketballThrowCommand command) => command.CommandId;
    public override string AggregateId(BasketballThrowCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(BasketballThrowCommand command) => command.ChatId;
    public override string DisplayName(BasketballThrowCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(BasketballThrowCommand command) => new(command.UserId, command.ChatId);
    protected override long UserId(BasketballThrowCommand command) => command.UserId;
}
