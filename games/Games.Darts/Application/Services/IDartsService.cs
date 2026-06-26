// ─────────────────────────────────────────────────────────────────────────────
// DartsService — place dart bets (queued rounds), resolve on bot 🎯 outcome.
// Payout: 4→x1, 5→x2, 6 (bullseye)→x2. Bot dice is sent per-chat serialized via
// <see cref="DartsRollDispatcherJob"/> + <see cref="DartsBotDiceSender"/>.
// ─────────────────────────────────────────────────────────────────────────────


namespace Games.Darts.Application.Services;

public interface IDartsService
{
    Task<DartsBetResult> PlaceBetAsync(
        long userId, string displayName, long chatId, int amount, int replyToMessageId, CancellationToken ct);

    Task<DartsThrowResult> ThrowAsync(
        long roundId, long userId, string displayName, long chatId, int botDiceMessageId, int face,
        CancellationToken ct);

    /// <summary>
    /// Quick-play: atomically place a default bet and settle it with <paramref name="face"/> from
    /// a user-thrown sticker (no bot dice, no queue). Used when the user sends the emoji directly.
    /// </summary>
    Task<DartsThrowResult> QuickThrowAsync(
        long userId, string displayName, long chatId, int diceMessageId, int face, int amount, CancellationToken ct);

    /// <summary>Refund and remove queued round if we could not send the bet-accepted reply after debit.</summary>
    Task AbortQueuedRoundIfBetReplyFailedAsync(long roundId, long userId, long chatId, CancellationToken ct);
}
