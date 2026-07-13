using BotFramework.Host.Execution;

namespace Games.Pick.Application.Execution;

public sealed class PickExecutionDescriptor : GameExecutionDescriptor<PickCommand, PickGameState, PickResult>
{
    public override string GameId => "pick";
    public override IReadOnlyList<string> EntropyNames => [PickAction.OutcomeEntropy];
    public override string CommandId(PickCommand command) => command.CommandId;
    public override string AggregateId(PickCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(PickCommand command) => command.ChatId;
    public override string DisplayName(PickCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(PickCommand command) => new(command.UserId, command.ChatId);
}
