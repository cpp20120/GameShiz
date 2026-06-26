
namespace Games.SecretHitler.Domain.Results;

public sealed record ShEnactResult(ShError Error, ShGameSnapshot? Snapshot, ShAfterEnactResult? After);
