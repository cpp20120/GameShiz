
namespace Games.SecretHitler.Domain.Results;

public sealed record ShCreateResult(ShError Error, string InviteCode, int BuyIn);
