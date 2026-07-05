namespace Games.Challenges.Transport.Grpc;

internal sealed record ChallengeActorCall(Guid ChallengeId, long ActorId);
