using System.Globalization;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;

namespace Games.Blackjack.Application.Execution;

public sealed class BlackjackSetMessageDescriptor
    : GameExecutionDescriptor<BlackjackSetMessageCommand, BlackjackGameState, BlackjackResult>
{
    public override string GameId => "blackjack";
    public override string CommandId(BlackjackSetMessageCommand command) => command.CommandId;
    public override string AggregateId(BlackjackSetMessageCommand command) => command.UserId.ToString(CultureInfo.InvariantCulture);
    public override long ChatId(BlackjackSetMessageCommand command) => command.ChatId;
    public override string DisplayName(BlackjackSetMessageCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(BlackjackSetMessageCommand command) => new(command.UserId, command.ChatId);
    public override BlackjackGameState CreateInitialState(BlackjackSetMessageCommand command) =>
        new(0, TurnGameStatus.Completed, command.UserId, null, command.DisplayName, null);
}
