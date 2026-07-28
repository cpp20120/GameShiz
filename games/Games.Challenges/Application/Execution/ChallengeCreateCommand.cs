namespace Games.Challenges.Application.Execution;

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
