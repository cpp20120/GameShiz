namespace Games.Poker.Application.Execution;

public sealed record PokerWalletRef(long UserId, long ChatId);

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

public sealed record PokerExecutionState(
    PokerTable? Table,
    List<PokerSeat> Seats,
    int? ActorBalance);

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

public sealed record PokerJoinCommand(
    string InviteCode,
    long ActorUserId,
    string DisplayName,
    long ChatId,
    string CommandId,
    int BuyIn,
    int MaxPlayers,
    IReadOnlyList<PokerWalletRef> ExpectedWallets) : IPokerExecutionCommand
{
    public bool EnsureActorWallet => true;
}

public sealed record PokerStartCommand(
    string InviteCode,
    long ActorUserId,
    string DisplayName,
    long ChatId,
    string CommandId,
    IReadOnlyList<PokerWalletRef> ExpectedWallets) : IPokerExecutionCommand
{
    public bool EnsureActorWallet => false;
}

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

public sealed record PokerAutoTurnCommand(
    string InviteCode,
    long ActorUserId,
    string DisplayName,
    long ChatId,
    string CommandId,
    IReadOnlyList<PokerWalletRef> ExpectedWallets) : IPokerExecutionCommand
{
    public bool EnsureActorWallet => false;
}

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

public sealed record PokerSetMessageCommand(
    string InviteCode,
    long ActorUserId,
    string DisplayName,
    long ChatId,
    string CommandId,
    int MessageId,
    IReadOnlyList<PokerWalletRef> ExpectedWallets) : IPokerExecutionCommand
{
    public bool EnsureActorWallet => false;
}
