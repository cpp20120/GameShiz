// ─────────────────────────────────────────────────────────────────────────────
// DiceCubeService — place a cube bet, resolve on 🎲 throw.
//
// Payout: credit = bet × mult(face). Defaults 1,2,2 ⇒ uniform d6 EV = 5/6 of stake
// (house +EV; old 1,2,3 was break-even for the house).
//
// MinSecondsBetweenBets: optional per-(user, chat) delay after a completed roll
// before the next /dice bet to reduce leaderboard / chat spam.
//
// Mult4/5/6 are snapshotted on the bet row so pending rolls keep the odds from
// when the bet was placed after runtime config changes.
// ─────────────────────────────────────────────────────────────────────────────


namespace Games.DiceCube.Application.Services;

public interface IDiceCubeService
{
    Task<CubeBetResult> PlaceBetAsync(long userId, string displayName, long chatId, int amount, CancellationToken ct);
    Task<CubeBetResult> PlaceBetAsync(long userId, string displayName, long chatId, int amount, int sourceMessageId, CancellationToken ct);
    Task<CubeRollResult> RollAsync(long userId, string displayName, long chatId, int face, CancellationToken ct);

    /// <summary>Refund and clear pending bet when bot cannot deliver SendMessage/SendDice after debit.</summary>
    Task AbortPendingBetAfterSendDiceFailedAsync(long userId, long chatId, CancellationToken ct);
}
