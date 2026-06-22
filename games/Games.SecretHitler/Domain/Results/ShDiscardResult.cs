using Games.SecretHitler.Domain;

namespace Games.SecretHitler.Domain.Results;

public sealed record ShDiscardResult(ShError Error, ShGameSnapshot? Snapshot);
