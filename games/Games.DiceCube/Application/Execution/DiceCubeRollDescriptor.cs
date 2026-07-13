using BotFramework.Host.Execution;
using Microsoft.Extensions.Options;

namespace Games.DiceCube.Application.Execution;

public sealed class DiceCubeRollDescriptor(
    IRuntimeTuningAccessor tuning,
    IOptions<BotFrameworkOptions> botOptions)
    : DiceCubeExecutionDescriptor<DiceCubeRollCommand, CubeRollResult>(tuning, botOptions)
{
    public override IReadOnlyList<string> EntropyNames => [DiceCubeRollAction.RedeemDropEntropy];
    public override string CommandId(DiceCubeRollCommand command) => command.CommandId;
    public override string AggregateId(DiceCubeRollCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(DiceCubeRollCommand command) => command.ChatId;
    public override string DisplayName(DiceCubeRollCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(DiceCubeRollCommand command) => new(command.UserId, command.ChatId);
    protected override long UserId(DiceCubeRollCommand command) => command.UserId;
}
