namespace Games.Challenges.Application.Execution;

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
