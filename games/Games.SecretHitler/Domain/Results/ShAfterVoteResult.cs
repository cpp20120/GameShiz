namespace Games.SecretHitler.Domain;

public sealed record ShAfterVoteResult(ShAfterVoteKind Kind, int JaVotes, int NeinVotes);
