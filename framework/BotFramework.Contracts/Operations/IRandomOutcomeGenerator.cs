namespace BotFramework.Contracts.Operations;

public interface IRandomOutcomeGenerator
{
    string AlgorithmVersion { get; }
    Task<FairnessCommitment> CommitAsync(string gameId, string canonicalInput,
        FairnessEntropySource entropySource = FairnessEntropySource.Server, CancellationToken ct = default);
    Task<FairnessResult> RevealAsync(long commitmentId, string canonicalInput, int exclusiveUpperBound,
        CancellationToken ct = default);
    FairnessVerification Verify(FairnessResult result, string canonicalInput, int exclusiveUpperBound);
    Task<IReadOnlyList<FairnessCommitment>> ListIncompleteAsync(CancellationToken ct = default);
}
