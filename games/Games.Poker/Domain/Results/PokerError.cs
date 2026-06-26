
namespace Games.Poker.Domain.Results;

public enum PokerError
{
    None = 0,
    NotEnoughCoins,
    AlreadySeated,
    TableNotFound,
    TableFull,
    HandInProgress,
    NotHost,
    NeedTwo,
    NoTable,
    NotYourTurn,
    CannotCheck,
    RaiseTooSmall,
    RaiseTooLarge,
    InvalidAction,
    TableAlreadyExists,
}
