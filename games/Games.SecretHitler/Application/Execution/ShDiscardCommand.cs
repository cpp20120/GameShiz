namespace Games.SecretHitler.Application.Execution;

public sealed record ShDiscardCommand(string Code, long UserId, string Name, long ChatId, long UserChatId,
    string Id, int DiscardIndex, IReadOnlyList<SecretHitlerWalletRef> Wallets)
    : ShTableCommand(Code, UserId, Name, ChatId, UserChatId, Id, Wallets);
