namespace Games.Poker.Domain.Rules;

public enum ValidationResult
{
    Ok = 0,
    CannotCheck,
    RaiseTooSmall,
    RaiseTooLarge,
    Invalid,
}
