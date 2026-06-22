namespace Games.Horse.Domain.Results;

public enum HorseError
{
    None = 0,
    InvalidHorseId,
    AmountNotSpecified,
    InvalidAmount,
    NotAdmin,
    NotEnoughBets,
}
