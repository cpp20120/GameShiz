using BotFramework.Host.Execution;

namespace Games.Challenges.Application.Execution;

public sealed class ChallengeAcceptDescriptor : ChallengeDescriptor<ChallengeAcceptCommand, ChallengeAcceptResult>
{
    public override bool UsesPrimaryWallet => false;
}
