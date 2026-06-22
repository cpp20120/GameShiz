using Games.SecretHitler.Domain;

namespace Games.SecretHitler;

public sealed record ShCreateResult(ShError Error, string InviteCode, int BuyIn);
