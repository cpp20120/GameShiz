using Games.Challenges.Domain.Entities;

namespace Games.Challenges.Transport.Grpc;

internal sealed record CreateChallengeCall(
    long ChallengerId,
    string ChallengerName,
    ChallengeUser Target,
    long ChatId,
    int Amount,
    ChallengeGame Game);
