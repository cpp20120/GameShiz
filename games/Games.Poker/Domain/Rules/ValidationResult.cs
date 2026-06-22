namespace Games.Poker.Domain;

public enum ValidationResult
{
    Ok = 0,
    CannotCheck,
    RaiseTooSmall,
    RaiseTooLarge,
    Invalid,
}
