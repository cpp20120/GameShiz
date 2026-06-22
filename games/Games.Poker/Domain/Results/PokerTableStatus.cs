// ─────────────────────────────────────────────────────────────────────────────
// PokerEntities — mutable domain state objects that double as Dapper rows.
// The domain (PokerDomain.Apply / ResolveAfterAction) mutates these in place,
// so fields are intentionally plain property setters rather than records.
// ─────────────────────────────────────────────────────────────────────────────

namespace Games.Poker.Domain.Results;

public enum PokerTableStatus
{
    Seating = 0,
    HandActive = 1,
    HandComplete = 2,
    Closed = 3,
}
