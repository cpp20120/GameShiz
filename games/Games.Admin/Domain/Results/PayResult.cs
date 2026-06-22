namespace Games.Admin.Domain.Results;

public sealed record PayResult(string DisplayName, int OldCoins, int NewCoins, int Amount);
