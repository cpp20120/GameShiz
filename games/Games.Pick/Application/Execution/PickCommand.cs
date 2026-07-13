namespace Games.Pick.Application.Execution;

public sealed record PickCommand(
    long UserId,
    string DisplayName,
    long ChatId,
    int Amount,
    IReadOnlyList<string> Variants,
    IReadOnlyList<int> BackedIndices,
    int Depth,
    bool ApplyStreak,
    string CommandId,
    int MinVariants,
    int MaxVariants,
    int MaxBet,
    double HouseEdge,
    double StreakBonusPerWin,
    int StreakCap,
    int ChainMaxDepth,
    int ChainTtlSeconds);
