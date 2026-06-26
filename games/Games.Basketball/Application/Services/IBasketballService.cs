// ─────────────────────────────────────────────────────────────────────────────
// BasketballService — place a 🏀 bet, resolve on throw.
// Telegram basketball dice values 1–5:
//   1-2 = rebound off rim/miss, 3 = bounces off ring, 4 = scored, 5 = clean swish.
// Payout: 4→x2, 5→x2. Uniform 1..5 die ⇒ EV 0.8 of stake.
// ─────────────────────────────────────────────────────────────────────────────


namespace Games.Basketball.Application.Services;

public interface IBasketballService
{
    Task<BasketballBetResult> PlaceBetAsync(long userId, string displayName, long chatId, int amount, CancellationToken ct);
    Task<BasketballBetResult> PlaceBetAsync(long userId, string displayName, long chatId, int amount, int sourceMessageId, CancellationToken ct);
    Task<BasketballThrowResult> ThrowAsync(long userId, string displayName, long chatId, int face, CancellationToken ct);

    /// <summary>Refund and clear pending bet when bot cannot complete SendMessage/SendDice after debit.</summary>
    Task AbortPendingBetAfterSendDiceFailedAsync(long userId, long chatId, CancellationToken ct);
}
