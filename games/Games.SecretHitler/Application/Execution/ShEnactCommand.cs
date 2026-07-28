namespace Games.SecretHitler.Application.Execution;

public sealed record ShEnactCommand(string Code, long UserId, string Name, long ChatId, long UserChatId,
    string Id, int EnactIndex, IReadOnlyList<SecretHitlerWalletRef> Wallets)
    : ShTableCommand(Code, UserId, Name, ChatId, UserChatId, Id, Wallets);
