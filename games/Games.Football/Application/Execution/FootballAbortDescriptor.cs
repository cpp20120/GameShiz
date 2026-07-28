using BotFramework.Host.Execution;
using Microsoft.Extensions.Options;

namespace Games.Football.Application.Execution;

public sealed class FootballAbortDescriptor(IRuntimeTuningAccessor tuning, IOptions<BotFrameworkOptions> options)
    : FootballExecutionDescriptor<FootballAbortCommand, FootballAbortResult>(tuning, options)
{
    public override string CommandId(FootballAbortCommand command) => command.CommandId;
    public override string AggregateId(FootballAbortCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(FootballAbortCommand command) => command.ChatId;
    public override string DisplayName(FootballAbortCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(FootballAbortCommand command) => new(command.UserId, command.ChatId);
    protected override long UserId(FootballAbortCommand command) => command.UserId;
}
