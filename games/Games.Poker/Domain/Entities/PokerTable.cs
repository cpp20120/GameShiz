// ─────────────────────────────────────────────────────────────────────────────
// PokerEntities — mutable domain state objects that double as Dapper rows.
// The domain (PokerDomain.Apply / ResolveAfterAction) mutates these in place,
// so fields are intentionally plain property setters rather than records.
// ─────────────────────────────────────────────────────────────────────────────

namespace Games.Poker.Domain.Entities;

public sealed class PokerTable
{
    public string InviteCode { get; set; } = "";
    public long ChatId { get; set; }
    public long HostUserId { get; set; }
    public PokerTableStatus Status { get; set; } = PokerTableStatus.Seating;
    public PokerPhase Phase { get; set; } = PokerPhase.None;
    public int SmallBlind { get; set; }
    public int BigBlind { get; set; }
    public int Pot { get; set; }
    public string CommunityCards { get; set; } = "";
    public string DeckState { get; set; } = "";
    public int ButtonSeat { get; set; }
    public int CurrentSeat { get; set; }
    public int CurrentBet { get; set; }
    public int MinRaise { get; set; }
    public int? StateMessageId { get; set; }
    public long LastActionAt { get; set; }
    public long CreatedAt { get; set; }
}
