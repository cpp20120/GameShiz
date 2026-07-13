using System.Globalization;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;

namespace Games.Blackjack.Application.Execution;

public sealed class BlackjackStartDescriptor
    : GameExecutionDescriptor<BlackjackStartCommand, BlackjackGameState, BlackjackResult>
{
    public override string GameId => "blackjack";
    public override IReadOnlyList<string> EntropyNames => BlackjackDecisionRules.ShuffleEntropyNames;
    public override string CommandId(BlackjackStartCommand command) => command.CommandId;
    public override string AggregateId(BlackjackStartCommand command) => command.UserId.ToString(CultureInfo.InvariantCulture);
    public override long ChatId(BlackjackStartCommand command) => command.ChatId;
    public override string DisplayName(BlackjackStartCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(BlackjackStartCommand command) => new(command.UserId, command.ChatId);
    public override BlackjackGameState CreateInitialState(BlackjackStartCommand command) =>
        new(0, TurnGameStatus.Completed, command.UserId, null, command.DisplayName, null);
}
