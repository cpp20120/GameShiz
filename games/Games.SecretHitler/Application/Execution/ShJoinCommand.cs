namespace Games.SecretHitler.Application.Execution;

public sealed record ShJoinCommand(string InviteCode, long ActorUserId, string DisplayName,
    long PublicChatId, long ActorChatId, string CommandId, int BuyIn,
    IReadOnlyList<SecretHitlerWalletRef> ExpectedWallets) : ISecretHitlerExecutionCommand
{
    public bool EnsureActorWallet => true;
}
