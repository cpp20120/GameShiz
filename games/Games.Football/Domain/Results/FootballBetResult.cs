namespace Games.Football;

public sealed record FootballBetResult(
    FootballBetError Error,
    int Amount = 0,
    int Balance = 0,
    int PendingAmount = 0,
    string? BlockingGameId = null,
    int DailyRollUsed = 0,
    int DailyRollLimit = 0)
{
    public static FootballBetResult Fail(FootballBetError err, int balance = 0, int pendingAmount = 0) =>
        new(err, 0, balance, pendingAmount, null, 0, 0);
}
