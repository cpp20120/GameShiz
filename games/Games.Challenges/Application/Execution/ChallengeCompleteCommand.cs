namespace Games.Challenges.Application.Execution;

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
