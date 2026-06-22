using Games.SecretHitler.Domain;

namespace Games.SecretHitler.Domain.Results;

public enum ShError
{
    None = 0,
    NotEnoughCoins,
    AlreadyInGame,
    GameNotFound,
    GameFull,
    GameInProgress,
    NotHost,
    NotInGame,
    NotEnoughPlayers,
    WrongPhase,
    NotPresident,
    NotChancellor,
    InvalidTarget,
    InvalidPolicy,
    TermLimited,
    AlreadyVoted,
}
