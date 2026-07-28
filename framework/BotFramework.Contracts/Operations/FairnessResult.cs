namespace BotFramework.Contracts.Operations;

public sealed record FairnessResult(long Id, string GameId, string AlgorithmVersion,
    string Commitment, string CanonicalInputHash, string RevealedSeed, string ResultHash,
    int Result, FairnessEntropySource EntropySource, FairnessAuditStatus Status,
    DateTimeOffset CreatedAt, DateTimeOffset CompletedAt);
