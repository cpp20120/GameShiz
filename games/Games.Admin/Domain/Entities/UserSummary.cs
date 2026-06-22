namespace Games.Admin.Domain.Entities;

public sealed record UserSummary(
    long TelegramUserId, long BalanceScopeId, string DisplayName, int Coins, long UpdatedAtUnixMs);
