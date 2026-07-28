namespace Games.Poker.Application.Execution;

public interface IPokerExecutionCommand
{
    string InviteCode { get; }
    long ChatId { get; }
    long ActorUserId { get; }
    string DisplayName { get; }
    string CommandId { get; }
    IReadOnlyList<PokerWalletRef> ExpectedWallets { get; }
    bool EnsureActorWallet { get; }
}
