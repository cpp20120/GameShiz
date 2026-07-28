namespace Games.SecretHitler.Application.Execution;

public abstract record ShTableCommand(string InviteCode, long ActorUserId, string DisplayName,
    long PublicChatId, long ActorChatId, string CommandId,
    IReadOnlyList<SecretHitlerWalletRef> ExpectedWallets) : ISecretHitlerExecutionCommand
{
    public bool EnsureActorWallet => false;
}
