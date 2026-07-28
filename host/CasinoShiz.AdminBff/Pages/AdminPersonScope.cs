namespace CasinoShiz.AdminBff.Pages;

public sealed record AdminPersonScope(long ScopeId, string Label, int Coins, DateTimeOffset LastActive);
