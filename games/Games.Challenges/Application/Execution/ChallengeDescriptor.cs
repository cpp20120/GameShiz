using BotFramework.Host.Execution;

namespace Games.Challenges.Application.Execution;

public abstract class ChallengeDescriptor<TCommand, TResult>
    : GameExecutionDescriptor<TCommand, ChallengeExecutionState, TResult>
    where TCommand : IChallengeExecutionCommand
{
    public override string GameId => "challenges";
    public override string CommandId(TCommand command) => command.CommandId;
    public override string AggregateId(TCommand command) => command.ChallengeId.ToString("N");
    public override long ChatId(TCommand command) => command.ChatId;
    public override string DisplayName(TCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(TCommand command) => new(command.ActorUserId, command.ChatId);
    public override IReadOnlyList<string> AdditionalLockKeys(TCommand command) =>
        command.ExpectedWallets.Select(wallet => new WalletIdentity(wallet.UserId, wallet.ChatId).LockKey)
            .Distinct(StringComparer.Ordinal).ToArray();
}
