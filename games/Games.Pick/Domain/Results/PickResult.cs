namespace Games.Pick;

public sealed record PickResult(
    PickError Error,
    int Bet,
    int Balance,
    int Payout,
    int Net,
    int StreakBonus,
    int StreakBefore,
    int StreakAfter,
    int PickedIndex,
    bool Won,
    int ChainDepth,
    Guid? ChainGuid,
    IReadOnlyList<string> Variants,
    IReadOnlyList<int> BackedIndices);
