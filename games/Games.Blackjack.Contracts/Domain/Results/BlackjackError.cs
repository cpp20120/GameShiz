namespace Games.Blackjack.Domain.Results;

public enum BlackjackError
{
    None = 0,
    InvalidBet,
    NotEnoughCoins,
    HandInProgress,
    NoActiveHand,
    CannotDouble,
}
