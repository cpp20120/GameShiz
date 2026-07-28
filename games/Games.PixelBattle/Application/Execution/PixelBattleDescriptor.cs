using System.Globalization;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;

namespace Games.PixelBattle.Application.Execution;

public sealed class PixelBattleDescriptor
    : GameExecutionDescriptor<PixelBattleCommand, PixelBattleExecutionState, PixelUpdateResult>
{
    public override string GameId => "pixelbattle";
    public override bool UsesPrimaryWallet => false;
    public override string CommandId(PixelBattleCommand command) => command.CommandId;
    public override string AggregateId(PixelBattleCommand command) => $"tile:{command.Index}";
    public override long ChatId(PixelBattleCommand command) => 0;
    public override string DisplayName(PixelBattleCommand command) => "pixelbattle user";
    public override WalletIdentity Wallet(PixelBattleCommand command) => new(command.UserId, 0);
}
