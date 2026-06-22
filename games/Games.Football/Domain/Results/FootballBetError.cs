namespace Games.Football.Domain.Results;

public enum FootballBetError
{
    None,
    InvalidAmount,
    NotEnoughCoins,
    AlreadyPending,
    BusyOtherGame,
    DailyRollLimit,
}
