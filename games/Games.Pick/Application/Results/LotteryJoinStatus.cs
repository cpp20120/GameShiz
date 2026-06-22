// ─────────────────────────────────────────────────────────────────────────────
// PickLotteryService — orchestrates open / join / draw / cancel for the
// /picklottery game. Talks to PickLotteryStore for persistence and to
// IEconomicsService for stake debits and payouts.
//
// Key invariant: every "stake" the user pays is debited BEFORE we attempt the
// DB write that would commit them to the pool. If the DB write loses to a
// race (or rejects), we IMMEDIATELY refund. There is never a window where
// the user has paid but isn't in a pool.
//
// Drawing (called from the sweeper):
//   1. List entries.
//   2. If count < MinEntrantsToSettle → MarkCancelledAsync, refund all stakes.
//   3. Else → pick winner uniformly, fee = floor(pot × HouseFeePercent),
//      payout = pot − fee, credit winner, MarkSettledAsync.
//
// All drawing/cancellation paths return a typed result so the caller (the
// sweeper job) can post the right message to the chat.
// ─────────────────────────────────────────────────────────────────────────────

using BotFramework.Host;
using Microsoft.Extensions.Options;

namespace Games.Pick.Application.Results;

public enum LotteryJoinStatus
{
    Ok,
    NoOpenLottery,
    AlreadyJoined,
    NotEnoughCoins,
    Failed,
}
