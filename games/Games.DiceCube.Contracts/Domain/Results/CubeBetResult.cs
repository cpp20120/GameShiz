namespace Games.DiceCube.Domain.Results;

public sealed record CubeBetResult(
    CubeBetError Error,
    int Amount = 0,
    int Balance = 0,
    int PendingAmount = 0,
    int CooldownSeconds = 0,
    string? BlockingGameId = null,
    int DailyRollUsed = 0,
    int DailyRollLimit = 0)
{
    public static CubeBetResult Fail(CubeBetError err, int balance = 0, int pendingAmount = 0) =>
        new(err, 0, balance, pendingAmount, 0, BlockingGameId: null, 0, 0);

    public static CubeBetResult CooldownWait(int balance, int waitSeconds) =>
        new(CubeBetError.Cooldown, 0, balance, 0, Math.Max(1, waitSeconds), BlockingGameId: null, 0, 0);
}
