namespace Games.Bowling.Domain.Results;

public enum BowlingBetError
{
    None,
    InvalidAmount,
    NotEnoughCoins,
    AlreadyPending,
    BusyOtherGame,
    DailyRollLimit,
}
