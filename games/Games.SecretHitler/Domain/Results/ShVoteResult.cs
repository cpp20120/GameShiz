using Games.SecretHitler.Domain;

namespace Games.SecretHitler.Domain.Results;

public sealed record ShVoteResult(ShError Error, ShGameSnapshot? Snapshot, ShAfterVoteResult? After);
