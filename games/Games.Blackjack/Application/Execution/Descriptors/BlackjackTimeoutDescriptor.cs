using System.Globalization;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;

namespace Games.Blackjack.Application.Execution;

public sealed class BlackjackTimeoutDescriptor
    : GameExecutionDescriptor<BlackjackTimeoutCommand, BlackjackGameState, BlackjackResult>
{
    public override string GameId => "blackjack";
    public override string CommandId(BlackjackTimeoutCommand command) => command.CommandId;
    public override string AggregateId(BlackjackTimeoutCommand command) => command.UserId.ToString(CultureInfo.InvariantCulture);
    public override long ChatId(BlackjackTimeoutCommand command) => command.ChatId;
    public override string DisplayName(BlackjackTimeoutCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(BlackjackTimeoutCommand command) => new(command.UserId, command.ChatId);
    public override BlackjackGameState CreateInitialState(BlackjackTimeoutCommand command) =>
        new(0, TurnGameStatus.Completed, command.UserId, null, command.DisplayName, null);
}
