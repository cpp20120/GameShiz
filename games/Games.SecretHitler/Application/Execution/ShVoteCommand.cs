namespace Games.SecretHitler.Application.Execution;

public sealed record ShVoteCommand(string Code, long UserId, string Name, long ChatId, long UserChatId,
    string Id, ShVote Vote, IReadOnlyList<SecretHitlerWalletRef> Wallets)
    : ShTableCommand(Code, UserId, Name, ChatId, UserChatId, Id, Wallets);
