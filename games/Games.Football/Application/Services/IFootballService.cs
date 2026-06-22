// ─────────────────────────────────────────────────────────────────────────────
// FootballService — bet, then resolve on Telegram's ⚽ dice (values 1–5).
// Payout: 1–3 → x0, 4 → x2, 5 → x2. Uniform 1..5 ⇒ EV 0.8 of stake.
// ─────────────────────────────────────────────────────────────────────────────

using BotFramework.Host;
using BotFramework.Sdk;

namespace Games.Football;

public interface IFootballService
{
    Task<FootballBetResult> PlaceBetAsync(long userId, string displayName, long chatId, int amount, CancellationToken ct);
    Task<FootballBetResult> PlaceBetAsync(long userId, string displayName, long chatId, int amount, int sourceMessageId, CancellationToken ct);
    Task<FootballThrowResult> ThrowAsync(long userId, string displayName, long chatId, int face, CancellationToken ct);

    /// <summary>Refund and clear pending bet when bot cannot complete SendMessage/SendDice after debit.</summary>
    Task AbortPendingBetAfterSendDiceFailedAsync(long userId, long chatId, CancellationToken ct);
}
