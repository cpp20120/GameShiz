namespace Games.SecretHitler.Application.Execution;

public sealed record SecretHitlerExecutionState(
    SecretHitlerGame? Game,
    List<SecretHitlerPlayer> Players,
    int? ActorBalance,
    bool ActorAlreadyInGame,
    bool ChatAlreadyHasGame);
