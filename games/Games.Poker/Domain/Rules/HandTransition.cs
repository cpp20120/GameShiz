using Games.Poker.Domain;

namespace Games.Poker;

public enum HandTransition
{
    None = 0,
    HandStarted,
    PhaseAdvanced,
    TurnAdvanced,
    HandEnded,
}
