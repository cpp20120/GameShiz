namespace Games.Poker.Domain.Rules;

public enum TransitionKind
{
    TurnAdvanced,
    PhaseAdvanced,
    HandEndedLastStanding,
    HandEndedRunout,
    HandEndedShowdown,
}
