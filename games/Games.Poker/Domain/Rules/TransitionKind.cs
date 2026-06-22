namespace Games.Poker.Domain;

public enum TransitionKind
{
    TurnAdvanced,
    PhaseAdvanced,
    HandEndedLastStanding,
    HandEndedRunout,
    HandEndedShowdown,
}
