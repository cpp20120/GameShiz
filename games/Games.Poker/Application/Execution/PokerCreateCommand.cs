namespace Games.Poker.Application.Execution;

public sealed record PokerCreateCommand(
    long ActorUserId,
    string DisplayName,
    long ChatId,
    string CommandId,
    int BuyIn,
    int SmallBlind,
    int BigBlind,
    IReadOnlyList<PokerWalletRef> ExpectedWallets) : IPokerExecutionCommand
{
    public string InviteCode => "";
    public bool EnsureActorWallet => true;
}
