namespace Games.SecretHitler.Domain.Entities;

public sealed class SecretHitlerGame
{
    public string InviteCode { get; set; } = "";
    public long HostUserId { get; set; }
    public long ChatId { get; set; }
    public ShStatus Status { get; set; } = ShStatus.Lobby;
    public ShPhase Phase { get; set; } = ShPhase.None;

    public int LiberalPolicies { get; set; }
    public int FascistPolicies { get; set; }
    public int ElectionTracker { get; set; }

    public int CurrentPresidentPosition { get; set; }
    public int NominatedChancellorPosition { get; set; } = -1;
    public int LastElectedPresidentPosition { get; set; } = -1;
    public int LastElectedChancellorPosition { get; set; } = -1;

    public string DeckState { get; set; } = "";
    public string DiscardState { get; set; } = "";
    public string PresidentDraw { get; set; } = "";
    public string ChancellorReceived { get; set; } = "";

    public ShWinner Winner { get; set; } = ShWinner.None;
    public ShWinReason WinReason { get; set; } = ShWinReason.None;

    public int BuyIn { get; set; }
    public int Pot { get; set; }
    public int? StateMessageId { get; set; }

    public long CreatedAt { get; set; }
    public long LastActionAt { get; set; }
}
