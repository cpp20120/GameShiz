using Games.SecretHitler.Domain;

namespace Games.SecretHitler;

public sealed record ShStartResult(ShError Error, ShGameSnapshot? Snapshot);
