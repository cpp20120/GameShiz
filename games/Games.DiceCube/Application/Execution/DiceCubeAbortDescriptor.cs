using BotFramework.Host.Execution;
using Microsoft.Extensions.Options;

namespace Games.DiceCube.Application.Execution;

public sealed class DiceCubeAbortDescriptor(
    IRuntimeTuningAccessor tuning,
    IOptions<BotFrameworkOptions> botOptions)
    : DiceCubeExecutionDescriptor<DiceCubeAbortCommand, DiceCubeAbortResult>(tuning, botOptions)
{
    public override string CommandId(DiceCubeAbortCommand command) => command.CommandId;
    public override string AggregateId(DiceCubeAbortCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(DiceCubeAbortCommand command) => command.ChatId;
    public override string DisplayName(DiceCubeAbortCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(DiceCubeAbortCommand command) => new(command.UserId, command.ChatId);
    protected override long UserId(DiceCubeAbortCommand command) => command.UserId;
}
