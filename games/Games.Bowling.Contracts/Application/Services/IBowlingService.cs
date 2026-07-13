// ─────────────────────────────────────────────────────────────────────────────
// BowlingService — place a 🎳 bet, resolve on roll.
// Telegram bowling dice values 1–6:
//   1 = gutter, 2–3 = few pins, 4 = several pins, 5 = most pins, 6 = strike.
// Payout: 4→x1, 5→x2, 6 (strike)→x2 (uniform d6 EV 5/6 of stake; house +EV).
// ─────────────────────────────────────────────────────────────────────────────


namespace Games.Bowling.Application.Services;

public interface IBowlingService
{
    Task<BowlingBetResult> PlaceBetAsync(long userId, string displayName, long chatId, int amount, int sourceMessageId, CancellationToken ct);
    Task<BowlingRollResult> RollAsync(long userId, string displayName, long chatId, int face, CancellationToken ct);
    Task<BowlingRollResult> RollAsync(long userId, string displayName, long chatId, int face, int sourceMessageId, CancellationToken ct);

    /// <summary>Refund and clear pending bet when bot cannot complete SendMessage/SendDice after debit.</summary>
    Task AbortPendingBetAfterSendDiceFailedAsync(long userId, long chatId, CancellationToken ct);
    Task AbortPendingBetAfterSendDiceFailedAsync(long userId, string displayName, long chatId, int sourceMessageId, CancellationToken ct);
}
