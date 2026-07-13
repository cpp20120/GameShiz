using Games.Challenges.Domain.Entities;

namespace Games.Challenges.Transport.Grpc;

internal sealed record CompleteChallengeCall(
    Challenge Challenge,
    int ChallengerRoll,
    int TargetRoll);
