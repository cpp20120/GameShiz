// ─────────────────────────────────────────────────────────────────────────────
// BlackjackService — application service for the /blackjack game.
//
// Port of src/CasinoShiz.Core/Services/Blackjack/BlackjackService.cs:
//   • EF Core BlackjackHand row → BlackjackHandStore (Dapper).
//   • EconomicsService.Debit/Credit calls now take userId (not entity).
//   • Natural-blackjack at Start settles immediately without persisting the
//     hand (identical to the monolith's fast-path).
//
// Lifecycle: Start → [loop: Hit / Stand / Double] → Settle. Settle deletes the
// hand row, credits any payout, and publishes BlackjackHandCompleted.
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Games.Blackjack.Application.Services;

public interface IBlackjackService
{
    Task<BlackjackResult> StartAsync(long userId, string displayName, long chatId, int bet, string operationId, CancellationToken ct);
    Task<BlackjackResult> HitAsync(long userId, CancellationToken ct);
    Task<BlackjackResult> StandAsync(long userId, CancellationToken ct);
    Task<BlackjackResult> DoubleAsync(long userId, CancellationToken ct);
    Task<(BlackjackSnapshot? snapshot, int? stateMessageId)> GetSnapshotAsync(long userId, CancellationToken ct);
    Task SetStateMessageIdAsync(long userId, int messageId, CancellationToken ct);
}
