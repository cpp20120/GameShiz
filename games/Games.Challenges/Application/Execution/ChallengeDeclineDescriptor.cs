using BotFramework.Host.Execution;

namespace Games.Challenges.Application.Execution;

public sealed class ChallengeDeclineDescriptor : ChallengeDescriptor<ChallengeDeclineCommand, ChallengeAcceptError>
{
    public override bool UsesPrimaryWallet => false;
}
