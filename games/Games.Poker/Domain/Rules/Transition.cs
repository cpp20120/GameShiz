namespace Games.Poker.Domain;

public sealed record Transition(
    TransitionKind Kind,
    PokerPhase FromPhase,
    PokerPhase ToPhase,
    IReadOnlyList<ShowdownEntry>? Showdown = null);
