
namespace Games.Poker.Domain.Results;

public sealed record ActionResult(
    PokerError Error,
    TableSnapshot? Snapshot,
    HandTransition Transition,
    IReadOnlyList<ShowdownEntry>? Showdown,
    string? AutoActorName,
    AutoAction? AutoKind);
