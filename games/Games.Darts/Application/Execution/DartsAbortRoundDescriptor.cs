using BotFramework.Host.Execution;
using Microsoft.Extensions.Options;

namespace Games.Darts.Application.Execution;

public sealed class DartsAbortRoundDescriptor(IRuntimeTuningAccessor tuning, IOptions<BotFrameworkOptions> botOptions)
    : DartsQueuedDescriptor<DartsAbortRoundCommand, DartsAbortRoundResult>(tuning, botOptions)
{
    public override string CommandId(DartsAbortRoundCommand command) => command.CommandId;
    public override string AggregateId(DartsAbortRoundCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(DartsAbortRoundCommand command) => command.ChatId;
    public override string DisplayName(DartsAbortRoundCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(DartsAbortRoundCommand command) => new(command.UserId, command.ChatId);
    protected override long UserId(DartsAbortRoundCommand command) => command.UserId;
}
