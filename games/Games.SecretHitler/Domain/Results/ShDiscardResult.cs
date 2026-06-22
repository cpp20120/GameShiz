using Games.SecretHitler.Domain;

namespace Games.SecretHitler;

public sealed record ShDiscardResult(ShError Error, ShGameSnapshot? Snapshot);
