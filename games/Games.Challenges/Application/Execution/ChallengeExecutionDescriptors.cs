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

public sealed class ChallengeCreateDescriptor : ChallengeDescriptor<ChallengeCreateCommand, ChallengeCreateResult>;

public sealed class ChallengeAcceptDescriptor : ChallengeDescriptor<ChallengeAcceptCommand, ChallengeAcceptResult>
{
    public override bool UsesPrimaryWallet => false;
}

public sealed class ChallengeDeclineDescriptor : ChallengeDescriptor<ChallengeDeclineCommand, ChallengeAcceptError>
{
    public override bool UsesPrimaryWallet => false;
}

public sealed class ChallengeCompleteDescriptor : ChallengeDescriptor<ChallengeCompleteCommand, ChallengeAcceptResult>
{
    public override bool UsesPrimaryWallet => false;
}

public sealed class ChallengeFailDescriptor : ChallengeDescriptor<ChallengeFailCommand, bool>
{
    public override bool UsesPrimaryWallet => false;
}
