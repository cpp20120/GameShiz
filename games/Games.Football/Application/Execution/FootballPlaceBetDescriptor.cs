using BotFramework.Host.Execution;
using Microsoft.Extensions.Options;

namespace Games.Football.Application.Execution;

public sealed class FootballPlaceBetDescriptor(IRuntimeTuningAccessor tuning, IOptions<BotFrameworkOptions> options)
    : FootballExecutionDescriptor<FootballPlaceBetCommand, FootballBetResult>(tuning, options)
{
    public override string CommandId(FootballPlaceBetCommand command) => command.CommandId;
    public override string AggregateId(FootballPlaceBetCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(FootballPlaceBetCommand command) => command.ChatId;
    public override string DisplayName(FootballPlaceBetCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(FootballPlaceBetCommand command) => new(command.UserId, command.ChatId);
    protected override long UserId(FootballPlaceBetCommand command) => command.UserId;
}
