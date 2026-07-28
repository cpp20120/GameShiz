using BotFramework.Host.Execution;

namespace Games.Challenges.Application.Execution;

public sealed class ChallengeFailDescriptor : ChallengeDescriptor<ChallengeFailCommand, bool>
{
    public override bool UsesPrimaryWallet => false;
}
