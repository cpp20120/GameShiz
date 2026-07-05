namespace Games.Pick.Domain.Results;

public enum PickError
{
    None,
    InvalidAmount,
    NotEnoughVariants,
    TooManyVariants,
    InvalidChoice,
    NotEnoughCoins,
    ChainExpired,
    ChainBusy,
}
