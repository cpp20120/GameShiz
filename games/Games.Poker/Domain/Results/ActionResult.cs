
namespace Games.Poker.Domain.Results;

public sealed record ActionResult(
    PokerError Error,
    TableSnapshot? Snapshot,
    HandTransition Transition,
    List<ShowdownEntry>? Showdown,
    string? AutoActorName,
    AutoAction? AutoKind);
