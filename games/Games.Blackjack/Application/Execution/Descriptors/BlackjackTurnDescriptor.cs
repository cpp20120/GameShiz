using System.Globalization;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;

namespace Games.Blackjack.Application.Execution;

public sealed class BlackjackTurnDescriptor
    : GameExecutionDescriptor<BlackjackTurnCommand, BlackjackGameState, BlackjackResult>
{
    public override string GameId => "blackjack";
    public override string CommandId(BlackjackTurnCommand command) => command.CommandId;
    public override string AggregateId(BlackjackTurnCommand command) => command.UserId.ToString(CultureInfo.InvariantCulture);
    public override long ChatId(BlackjackTurnCommand command) => command.ChatId;
    public override string DisplayName(BlackjackTurnCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(BlackjackTurnCommand command) => new(command.UserId, command.ChatId);
    public override BlackjackGameState CreateInitialState(BlackjackTurnCommand command) =>
        new(0, TurnGameStatus.Completed, command.UserId, null, command.DisplayName, null);
}
