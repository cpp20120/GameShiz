// ─────────────────────────────────────────────────────────────────────────────
// PickLotteryStore — Dapper queries for the /picklottery game.
//
// Locking strategy:
//   • Open / join writes go through a transaction with FOR UPDATE on the
//     pick_lottery row to keep entry counts and refund states consistent.
//   • The unique partial index ux_pick_lottery_open_per_chat is the canonical
//     guarantee that there is at most one open pool per chat — race-free at
//     the DB level, no application-side locking required.
// ─────────────────────────────────────────────────────────────────────────────

using BotFramework.Host;
using Dapper;

namespace Games.Pick;

public enum LotteryOpenError
{
    None,
    AlreadyOpenInChat,
    DbError,
}
