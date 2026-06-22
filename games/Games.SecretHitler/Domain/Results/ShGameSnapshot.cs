using Games.SecretHitler.Domain;

namespace Games.SecretHitler.Domain.Results;

public sealed record ShGameSnapshot(SecretHitlerGame Game, List<SecretHitlerPlayer> Players);
