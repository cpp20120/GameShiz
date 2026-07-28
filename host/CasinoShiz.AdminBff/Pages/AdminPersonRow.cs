namespace CasinoShiz.AdminBff.Pages;

public sealed record AdminPersonRow(long UserId, string DisplayName, int WalletCount, long TotalCoins,
    DateTimeOffset LastActive, IReadOnlyList<AdminPersonScope> Scopes);
