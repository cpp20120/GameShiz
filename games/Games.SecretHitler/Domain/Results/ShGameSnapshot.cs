using Games.SecretHitler.Domain;

namespace Games.SecretHitler;

public sealed record ShGameSnapshot(SecretHitlerGame Game, List<SecretHitlerPlayer> Players);
