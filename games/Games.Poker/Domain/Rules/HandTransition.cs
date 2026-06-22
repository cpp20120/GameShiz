using Games.Poker.Domain;

namespace Games.Poker.Domain.Rules;

public enum HandTransition
{
    None = 0,
    HandStarted,
    PhaseAdvanced,
    TurnAdvanced,
    HandEnded,
}
