namespace Games.Poker.Application.Execution;

public sealed record PokerPlayerTurnCommand(
    string InviteCode,
    long ActorUserId,
    string DisplayName,
    long ChatId,
    string CommandId,
    string Verb,
    int Amount,
    IReadOnlyList<PokerWalletRef> ExpectedWallets) : IPokerExecutionCommand
{
    public bool EnsureActorWallet => false;
}
