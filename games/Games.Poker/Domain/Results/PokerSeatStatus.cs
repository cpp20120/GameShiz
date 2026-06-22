// ─────────────────────────────────────────────────────────────────────────────
// PokerEntities — mutable domain state objects that double as Dapper rows.
// The domain (PokerDomain.Apply / ResolveAfterAction) mutates these in place,
// so fields are intentionally plain property setters rather than records.
// ─────────────────────────────────────────────────────────────────────────────

namespace Games.Poker.Domain.Results;

public enum PokerSeatStatus
{
    Seated = 0,
    Folded = 1,
    AllIn = 2,
    SittingOut = 3,
}
