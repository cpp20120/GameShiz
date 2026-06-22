using Games.SecretHitler.Domain;

namespace Games.SecretHitler;

public sealed record ShNominateResult(ShError Error, ShGameSnapshot? Snapshot);
