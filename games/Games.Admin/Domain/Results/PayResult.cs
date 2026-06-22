namespace Games.Admin;

public sealed record PayResult(string DisplayName, int OldCoins, int NewCoins, int Amount);
