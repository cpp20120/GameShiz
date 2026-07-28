namespace BotFramework.Contracts.Operations;

public sealed record FairnessCommitment(long Id, string GameId, string AlgorithmVersion,
    string Commitment, string CanonicalInputHash, FairnessEntropySource EntropySource,
    DateTimeOffset CreatedAt);
