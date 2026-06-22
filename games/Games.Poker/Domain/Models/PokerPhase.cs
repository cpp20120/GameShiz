// ─────────────────────────────────────────────────────────────────────────────
// PokerEntities — mutable domain state objects that double as Dapper rows.
// The domain (PokerDomain.Apply / ResolveAfterAction) mutates these in place,
// so fields are intentionally plain property setters rather than records.
// ─────────────────────────────────────────────────────────────────────────────

namespace Games.Poker;

public enum PokerPhase
{
    None = 0,
    PreFlop = 1,
    Flop = 2,
    Turn = 3,
    River = 4,
    Showdown = 5,
}
