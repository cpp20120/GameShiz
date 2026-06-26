
namespace Games.SecretHitler.Domain.Results;

public sealed record ShJoinResult(ShError Error, ShGameSnapshot? Snapshot, int Joined, int Max);
