using Games.Poker.Domain;

namespace Games.Poker;

public sealed record ActionResult(
    PokerError Error,
    TableSnapshot? Snapshot,
    HandTransition Transition,
    List<ShowdownEntry>? Showdown,
    string? AutoActorName,
    AutoAction? AutoKind);
