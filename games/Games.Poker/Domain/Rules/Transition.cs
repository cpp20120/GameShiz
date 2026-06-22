namespace Games.Poker.Domain.Rules;

public sealed record Transition(
    TransitionKind Kind,
    PokerPhase FromPhase,
    PokerPhase ToPhase,
    IReadOnlyList<ShowdownEntry>? Showdown = null);
