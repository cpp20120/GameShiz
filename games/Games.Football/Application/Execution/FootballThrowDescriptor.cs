using BotFramework.Host.Execution;
using Microsoft.Extensions.Options;

namespace Games.Football.Application.Execution;

public sealed class FootballThrowDescriptor(IRuntimeTuningAccessor tuning, IOptions<BotFrameworkOptions> options)
    : FootballExecutionDescriptor<FootballThrowCommand, FootballThrowResult>(tuning, options)
{
    public override IReadOnlyList<string> EntropyNames => [FootballThrowAction.RedeemDropEntropy];
    public override string CommandId(FootballThrowCommand command) => command.CommandId;
    public override string AggregateId(FootballThrowCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(FootballThrowCommand command) => command.ChatId;
    public override string DisplayName(FootballThrowCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(FootballThrowCommand command) => new(command.UserId, command.ChatId);
    protected override long UserId(FootballThrowCommand command) => command.UserId;
}
