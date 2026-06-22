using Games.SecretHitler.Domain;

namespace Games.SecretHitler;

public sealed record ShEnactResult(ShError Error, ShGameSnapshot? Snapshot, ShAfterEnactResult? After);
