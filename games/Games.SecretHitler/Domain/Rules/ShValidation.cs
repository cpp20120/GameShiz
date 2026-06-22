namespace Games.SecretHitler.Domain.Rules;

public enum ShValidation
{
    Ok,
    NotPresident,
    NotChancellor,
    NotYourTurn,
    InvalidTarget,
    TermLimited,
    AlreadyVoted,
    WrongPhase,
    InvalidPolicy,
}
