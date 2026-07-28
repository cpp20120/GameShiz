using BotFramework.Host.Execution;

namespace Games.Challenges.Application.Execution;

public sealed class ChallengeCompleteDescriptor : ChallengeDescriptor<ChallengeCompleteCommand, ChallengeAcceptResult>
{
    public override bool UsesPrimaryWallet => false;
}
