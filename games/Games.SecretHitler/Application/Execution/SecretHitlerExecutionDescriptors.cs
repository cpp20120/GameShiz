using BotFramework.Host.Execution;

namespace Games.SecretHitler.Application.Execution;

public abstract class SecretHitlerDescriptor<TCommand, TResult>
    : GameExecutionDescriptor<TCommand, SecretHitlerExecutionState, TResult>
    where TCommand : ISecretHitlerExecutionCommand
{
    public override string GameId => "sh";
    public override bool UsesPrimaryWallet => false;
    public override string CommandId(TCommand command) => command.CommandId;
    public override string AggregateId(TCommand command) => string.IsNullOrEmpty(command.InviteCode)
        ? $"user:{command.ActorUserId}" : $"game:{command.InviteCode}";
    public override long ChatId(TCommand command) => command.PublicChatId;
    public override string DisplayName(TCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(TCommand command) => new(command.ActorUserId, command.ActorChatId);
    public override IReadOnlyList<string> AdditionalLockKeys(TCommand command)
    {
        var keys = command.ExpectedWallets
            .Select(wallet => new WalletIdentity(wallet.UserId, wallet.ChatId).LockKey).ToList();
        keys.Add($"sh:user:{command.ActorUserId}");
        if (string.IsNullOrEmpty(command.InviteCode)) keys.Add($"sh:chat:{command.PublicChatId}");
        return keys.Distinct(StringComparer.Ordinal).ToArray();
    }
}

public sealed class ShCreateDescriptor : SecretHitlerDescriptor<ShCreateCommand, ShCreateResult>
{
    public override IReadOnlyList<string> EntropyNames => [SecretHitlerExecutionRules.InviteEntropy];
}
public sealed class ShJoinDescriptor : SecretHitlerDescriptor<ShJoinCommand, ShJoinResult>;
public sealed class ShStartDescriptor : SecretHitlerDescriptor<ShStartCommand, ShStartResult>
{
    public override IReadOnlyList<string> EntropyNames =>
        [.. SecretHitlerExecutionRules.RoleEntropyNames, .. SecretHitlerExecutionRules.DeckEntropyNames];
}
public sealed class ShNominateDescriptor : SecretHitlerDescriptor<ShNominateCommand, ShNominateResult>;
public sealed class ShVoteDescriptor : SecretHitlerDescriptor<ShVoteCommand, ShVoteResult>
{
    public override IReadOnlyList<string> EntropyNames => SecretHitlerExecutionRules.ReshuffleEntropyNames;
}
public sealed class ShDiscardDescriptor : SecretHitlerDescriptor<ShDiscardCommand, ShDiscardResult>;
public sealed class ShEnactDescriptor : SecretHitlerDescriptor<ShEnactCommand, ShEnactResult>;
public sealed class ShLeaveDescriptor : SecretHitlerDescriptor<ShLeaveCommand, ShLeaveResult>;
public sealed class ShPlayerMessageDescriptor : SecretHitlerDescriptor<ShPlayerMessageCommand, bool>;
public sealed class ShPublicMessageDescriptor : SecretHitlerDescriptor<ShPublicMessageCommand, bool>;
