namespace Games.Admin;

public sealed record UserSummary(
    long TelegramUserId, long BalanceScopeId, string DisplayName, int Coins, long UpdatedAtUnixMs);
