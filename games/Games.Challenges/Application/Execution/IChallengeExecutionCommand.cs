namespace Games.Challenges.Application.Execution;

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
