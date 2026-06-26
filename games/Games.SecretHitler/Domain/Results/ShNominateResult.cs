
namespace Games.SecretHitler.Domain.Results;

public sealed record ShNominateResult(ShError Error, ShGameSnapshot? Snapshot);
