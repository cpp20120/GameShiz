using Games.SecretHitler.Domain;

namespace Games.SecretHitler;

public sealed record ShVoteResult(ShError Error, ShGameSnapshot? Snapshot, ShAfterVoteResult? After);
