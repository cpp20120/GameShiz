// ─────────────────────────────────────────────────────────────────────────────
// PickDailyLotteryStore — Dapper queries for the per-chat daily lottery.
//
// Tables:
//   pick_daily_lottery          (chat_id, day_local) UNIQUE, status state machine
//   pick_daily_lottery_tickets  one row per purchased ticket (FK + cascade)
//
// Race-safe ergonomics:
//   • GetOrCreateOpenAsync uses INSERT ... ON CONFLICT DO NOTHING + SELECT
//     so the first concurrent caller wins the insert and the rest just read.
//   • Settle uses UPDATE ... WHERE status='open' so a manually-cancelled row
//     can't double-settle.
// ─────────────────────────────────────────────────────────────────────────────

using BotFramework.Host;
using Dapper;

namespace Games.Pick.Infrastructure.Models;

public sealed record PickDailyTicketSummary(long UserId, string DisplayName, int TicketCount);
