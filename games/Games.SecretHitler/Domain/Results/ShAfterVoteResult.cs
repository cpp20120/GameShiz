namespace Games.SecretHitler.Domain.Results;

public sealed record ShAfterVoteResult(ShAfterVoteKind Kind, int JaVotes, int NeinVotes);
