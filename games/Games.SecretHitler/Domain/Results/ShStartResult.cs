
namespace Games.SecretHitler.Domain.Results;

public sealed record ShStartResult(ShError Error, ShGameSnapshot? Snapshot);
