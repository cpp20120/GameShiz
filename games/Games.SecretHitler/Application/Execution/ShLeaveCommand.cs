namespace Games.SecretHitler.Application.Execution;

public sealed record ShLeaveCommand(string Code, long UserId, string Name, long ChatId, long UserChatId,
    string Id, IReadOnlyList<SecretHitlerWalletRef> Wallets)
    : ShTableCommand(Code, UserId, Name, ChatId, UserChatId, Id, Wallets);
