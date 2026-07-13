namespace Games.SecretHitler.Application.Execution;

public sealed record SecretHitlerWalletRef(long UserId, long ChatId);

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

public sealed record SecretHitlerExecutionState(
    SecretHitlerGame? Game,
    List<SecretHitlerPlayer> Players,
    int? ActorBalance,
    bool ActorAlreadyInGame,
    bool ChatAlreadyHasGame);

public sealed record ShCreateCommand(long ActorUserId, string DisplayName, long PublicChatId,
    long ActorChatId, string CommandId, int BuyIn,
    IReadOnlyList<SecretHitlerWalletRef> ExpectedWallets) : ISecretHitlerExecutionCommand
{
    public string InviteCode => "";
    public bool EnsureActorWallet => true;
}

public sealed record ShJoinCommand(string InviteCode, long ActorUserId, string DisplayName,
    long PublicChatId, long ActorChatId, string CommandId, int BuyIn,
    IReadOnlyList<SecretHitlerWalletRef> ExpectedWallets) : ISecretHitlerExecutionCommand
{
    public bool EnsureActorWallet => true;
}

public abstract record ShTableCommand(string InviteCode, long ActorUserId, string DisplayName,
    long PublicChatId, long ActorChatId, string CommandId,
    IReadOnlyList<SecretHitlerWalletRef> ExpectedWallets) : ISecretHitlerExecutionCommand
{
    public bool EnsureActorWallet => false;
}

public sealed record ShStartCommand(string Code, long UserId, string Name, long ChatId, long UserChatId,
    string Id, IReadOnlyList<SecretHitlerWalletRef> Wallets)
    : ShTableCommand(Code, UserId, Name, ChatId, UserChatId, Id, Wallets);

public sealed record ShNominateCommand(string Code, long UserId, string Name, long ChatId, long UserChatId,
    string Id, int ChancellorPosition, IReadOnlyList<SecretHitlerWalletRef> Wallets)
    : ShTableCommand(Code, UserId, Name, ChatId, UserChatId, Id, Wallets);

public sealed record ShVoteCommand(string Code, long UserId, string Name, long ChatId, long UserChatId,
    string Id, ShVote Vote, IReadOnlyList<SecretHitlerWalletRef> Wallets)
    : ShTableCommand(Code, UserId, Name, ChatId, UserChatId, Id, Wallets);

public sealed record ShDiscardCommand(string Code, long UserId, string Name, long ChatId, long UserChatId,
    string Id, int DiscardIndex, IReadOnlyList<SecretHitlerWalletRef> Wallets)
    : ShTableCommand(Code, UserId, Name, ChatId, UserChatId, Id, Wallets);

public sealed record ShEnactCommand(string Code, long UserId, string Name, long ChatId, long UserChatId,
    string Id, int EnactIndex, IReadOnlyList<SecretHitlerWalletRef> Wallets)
    : ShTableCommand(Code, UserId, Name, ChatId, UserChatId, Id, Wallets);

public sealed record ShLeaveCommand(string Code, long UserId, string Name, long ChatId, long UserChatId,
    string Id, IReadOnlyList<SecretHitlerWalletRef> Wallets)
    : ShTableCommand(Code, UserId, Name, ChatId, UserChatId, Id, Wallets);

public sealed record ShPlayerMessageCommand(string Code, long UserId, string Name, long ChatId, long UserChatId,
    string Id, int MessageId, IReadOnlyList<SecretHitlerWalletRef> Wallets)
    : ShTableCommand(Code, UserId, Name, ChatId, UserChatId, Id, Wallets);

public sealed record ShPublicMessageCommand(string Code, long UserId, string Name, long ChatId, long UserChatId,
    string Id, int MessageId, IReadOnlyList<SecretHitlerWalletRef> Wallets)
    : ShTableCommand(Code, UserId, Name, ChatId, UserChatId, Id, Wallets);
