using Games.SecretHitler.Domain;

namespace Games.SecretHitler;

public sealed record ShLeaveResult(ShError Error, ShGameSnapshot? Snapshot, bool GameClosed);
