namespace Games.SecretHitler.Application.Execution;

public interface ISecretHitlerExecutionCommand
{
    string InviteCode { get; }
    long ActorUserId { get; }
    string DisplayName { get; }
    long PublicChatId { get; }
    long ActorChatId { get; }
    string CommandId { get; }
    IReadOnlyList<SecretHitlerWalletRef> ExpectedWallets { get; }
    bool EnsureActorWallet { get; }
}
