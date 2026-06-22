namespace Games.DiceCube.Domain.Results;

public enum CubeBetError
{
    None,
    InvalidAmount,
    NotEnoughCoins,
    AlreadyPending,
    Cooldown,
    BusyOtherGame,
    DailyRollLimit,
}
