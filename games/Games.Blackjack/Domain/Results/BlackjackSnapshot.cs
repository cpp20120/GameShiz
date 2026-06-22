namespace Games.Blackjack.Domain.Results;

public sealed record BlackjackSnapshot(
    string[] PlayerCards,
    string[] DealerCards,
    int PlayerTotal,
    int DealerTotal,
    int Bet,
    int PlayerCoins,
    bool DealerHoleRevealed,
    bool CanDouble,
    BlackjackOutcome? Outcome,
    int Payout);
