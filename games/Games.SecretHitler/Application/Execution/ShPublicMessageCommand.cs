namespace Games.SecretHitler.Application.Execution;

public sealed record ShPublicMessageCommand(string Code, long UserId, string Name, long ChatId, long UserChatId,
    string Id, int MessageId, IReadOnlyList<SecretHitlerWalletRef> Wallets)
    : ShTableCommand(Code, UserId, Name, ChatId, UserChatId, Id, Wallets);
