
namespace Games.SecretHitler.Domain.Results;

public sealed record ShLeaveResult(ShError Error, ShGameSnapshot? Snapshot, bool GameClosed);
