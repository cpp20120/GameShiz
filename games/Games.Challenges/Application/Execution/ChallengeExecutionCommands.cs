namespace Games.Challenges.Application.Execution;

public sealed record ChallengeWalletRef(long UserId, long ChatId);

public interface IChallengeExecutionCommand
{
    Guid ChallengeId { get; }
    long ChatId { get; }
    long ActorUserId { get; }
    string DisplayName { get; }
    string CommandId { get; }
    IReadOnlyList<ChallengeWalletRef> ExpectedWallets { get; }
    bool EnsureExpectedWallets { get; }
}

public sealed record ChallengeExecutionState(
    Challenge? Challenge,
    bool HasPendingPair,
    int ChallengerBalance,
    int TargetBalance);

public sealed record ChallengeCreateCommand(
    Guid ChallengeId,
    long ActorUserId,
    string DisplayName,
    ChallengeUser Target,
    long ChatId,
    int Amount,
    ChallengeGame Game,
    int MinBet,
    int MaxBet,
    TimeSpan PendingTtl,
    string CommandId,
    IReadOnlyList<ChallengeWalletRef> ExpectedWallets) : IChallengeExecutionCommand
{
    public bool EnsureExpectedWallets => false;
}

public sealed record ChallengeAcceptCommand(
    Guid ChallengeId,
    long ActorUserId,
    string DisplayName,
    long ChatId,
    string CommandId,
    IReadOnlyList<ChallengeWalletRef> ExpectedWallets) : IChallengeExecutionCommand
{
    public bool EnsureExpectedWallets => true;
}

public sealed record ChallengeDeclineCommand(
    Guid ChallengeId,
    long ActorUserId,
    string DisplayName,
    long ChatId,
    string CommandId,
    IReadOnlyList<ChallengeWalletRef> ExpectedWallets) : IChallengeExecutionCommand
{
    public bool EnsureExpectedWallets => false;
}

public sealed record ChallengeCompleteCommand(
    Guid ChallengeId,
    long ActorUserId,
    string DisplayName,
    long ChatId,
    int ChallengerRoll,
    int TargetRoll,
    int HouseFeeBasisPoints,
    string CommandId,
    IReadOnlyList<ChallengeWalletRef> ExpectedWallets) : IChallengeExecutionCommand
{
    public bool EnsureExpectedWallets => false;
}

public sealed record ChallengeFailCommand(
    Guid ChallengeId,
    long ActorUserId,
    string DisplayName,
    long ChatId,
    string CommandId,
    IReadOnlyList<ChallengeWalletRef> ExpectedWallets) : IChallengeExecutionCommand
{
    public bool EnsureExpectedWallets => false;
}
