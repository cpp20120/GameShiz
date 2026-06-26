namespace Games.Basketball.Domain.Results;

public sealed record BasketballBetResult(
    BasketballBetError Error,
    int Amount = 0,
    int Balance = 0,
    int PendingAmount = 0,
    string? BlockingGameId = null,
    int DailyRollUsed = 0,
    int DailyRollLimit = 0)
{
    public static BasketballBetResult Fail(BasketballBetError err, int balance = 0, int pendingAmount = 0) =>
        new(err, 0, balance, pendingAmount, BlockingGameId: null, 0, 0);
}
