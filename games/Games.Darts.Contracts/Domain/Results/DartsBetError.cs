namespace Games.Darts.Domain.Results;

public enum DartsBetError
{
    None,
    InvalidAmount,
    NotEnoughCoins,
    BusyOtherGame,
    DailyRollLimit,
}
