namespace Games.SecretHitler.Application.Execution;

public sealed record ShNominateCommand(string Code, long UserId, string Name, long ChatId, long UserChatId,
    string Id, int ChancellorPosition, IReadOnlyList<SecretHitlerWalletRef> Wallets)
    : ShTableCommand(Code, UserId, Name, ChatId, UserChatId, Id, Wallets);
