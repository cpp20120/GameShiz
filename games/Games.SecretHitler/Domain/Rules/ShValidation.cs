namespace Games.SecretHitler.Domain;

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
