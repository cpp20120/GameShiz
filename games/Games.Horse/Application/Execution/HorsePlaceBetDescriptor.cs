using BotFramework.Host.Execution;

namespace Games.Horse.Application.Execution;

public sealed class HorsePlaceBetDescriptor
    : GameExecutionDescriptor<HorsePlaceBetCommand, HorseBetState, BetResult>
{
    public override string GameId => "horse";
    public override string CommandId(HorsePlaceBetCommand command) => command.CommandId;
    public override string AggregateId(HorsePlaceBetCommand command) =>
        $"{command.RaceDate}:{command.BalanceScopeId}:{command.UserId}";
    public override long ChatId(HorsePlaceBetCommand command) => command.BalanceScopeId;
    public override string DisplayName(HorsePlaceBetCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(HorsePlaceBetCommand command) =>
        new(command.UserId, command.BalanceScopeId);
}
