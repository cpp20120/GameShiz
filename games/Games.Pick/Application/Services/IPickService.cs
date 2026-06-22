// ─────────────────────────────────────────────────────────────────────────────
// PickService — single-player /pick game.
//
// One round flow (top-level bet):
//   1. validate amount + variants + backed indices (1..N, deduped),
//   2. ensure wallet, check balance, debit stake (reason "pick.bet"),
//   3. roll uniform index in [0, N),
//   4. if rolled index ∈ backed: gross = bet × (N / k); credit floor(gross × (1 − HouseEdge)) + streak bonus,
//      streak++. Else: streak resets to 0.
//   5. if won and ChainMaxDepth > 0 and depth < cap: register a PickChainState
//      so the handler can show a "double or nothing" inline button.
//
// Chain hops (entered via ContinueChainAsync): same flow but skips streak math
// (chains are flavour, not progression) and uses the previous payout as the
// new stake.
//
// Balance scope = chat id (matches every other casino game in the host).
// ─────────────────────────────────────────────────────────────────────────────

using BotFramework.Host;
using Microsoft.Extensions.Options;

namespace Games.Pick.Application.Services;

public interface IPickService
{
    Task<PickResult> PickAsync(
        long userId,
        string displayName,
        long chatId,
        int amount,
        IReadOnlyList<string> variants,
        IReadOnlyList<int> backedIndices,
        CancellationToken ct);

    /// <summary>Continue an existing chain. Caller has already claimed the chain state.</summary>
    Task<PickResult> ContinueChainAsync(PickChainState chain, CancellationToken ct);
}
