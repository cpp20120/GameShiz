using Games.SecretHitler.Domain;

namespace Games.SecretHitler;

public sealed record ShJoinResult(ShError Error, ShGameSnapshot? Snapshot, int Joined, int Max);
