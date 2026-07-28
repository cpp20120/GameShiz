namespace Games.Poker.Application.Execution;

public sealed record PokerLeaveCommand(
    string InviteCode,
    long ActorUserId,
    string DisplayName,
    long ChatId,
    string CommandId,
    IReadOnlyList<PokerWalletRef> ExpectedWallets) : IPokerExecutionCommand
{
    public bool EnsureActorWallet => false;
}
