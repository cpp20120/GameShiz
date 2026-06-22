using System.Collections.Concurrent;

namespace Games.Pick.Infrastructure.Persistence;

/// <summary>
/// State for a pending double-or-nothing chain offer. The user has just won
/// <see cref="StakeForNext"/> coins and (optionally) wants to re-stake the
/// whole thing on the same variants/backing. Stored in memory keyed by a fresh
/// GUID that we embed in the inline button's callback data.
/// </summary>
public sealed record PickChainState(
    Guid Id,
    long UserId,
    long ChatId,
    string DisplayName,
    int StakeForNext,
    int Depth,
    IReadOnlyList<string> Variants,
    IReadOnlyList<int> BackedIndices,
    DateTimeOffset ExpiresAt);
