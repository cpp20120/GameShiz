using BotFramework.Host.Execution;

namespace Games.Poker.Application.Execution;

public abstract class PokerDescriptor<TCommand, TResult>
    : GameExecutionDescriptor<TCommand, PokerExecutionState, TResult>
    where TCommand : IPokerExecutionCommand
{
    public override string GameId => "poker";
    public override bool UsesPrimaryWallet => false;
    public override string CommandId(TCommand command) => command.CommandId;
    public override string AggregateId(TCommand command) => string.IsNullOrEmpty(command.InviteCode)
        ? $"chat:{command.ChatId}" : $"table:{command.InviteCode}";
    public override long ChatId(TCommand command) => command.ChatId;
    public override string DisplayName(TCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(TCommand command) => new(command.ActorUserId, command.ChatId);
    public override IReadOnlyList<string> AdditionalLockKeys(TCommand command) =>
        command.ExpectedWallets.Select(wallet => new WalletIdentity(wallet.UserId, wallet.ChatId).LockKey)
            .Distinct(StringComparer.Ordinal).ToArray();
}
