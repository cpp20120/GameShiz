namespace BotFramework.Contracts.Operations;

public sealed record ReplayStep(long Version, string EventType, long OccurredAt,
    bool Compatible, string PayloadHash, string? Diagnostic);
public sealed record EventReplayReport(string StreamId, IReadOnlyList<ReplayStep> Steps,
    long? FirstIncompatibleVersion, string? Diagnostic);

public interface IReadOnlyEventReplayService
{
    Task<EventReplayReport> ReplayAsync(string streamId, CancellationToken ct = default);
}

public sealed record EconomyRulesSnapshot(int StartingBalance, int Stake, int WinPayout,
    double WinProbability, int InflationWarningThreshold);
public sealed record EconomySimulationRequest(EconomyRulesSnapshot Rules, int Players, int Rounds, int Seed);
public sealed record EconomySimulationReport(long Emission, long Sinks, double Rtp,
    IReadOnlyList<int> FinalBalances, IReadOnlyList<string> Warnings);

public interface IEconomySimulationService
{
    EconomySimulationReport Simulate(EconomySimulationRequest request);
}

public enum FairnessEntropySource { Server, External }
public enum FairnessAuditStatus { Committed, Completed, Abandoned }
public sealed record FairnessCommitment(long Id, string GameId, string AlgorithmVersion,
    string Commitment, string CanonicalInputHash, FairnessEntropySource EntropySource,
    DateTimeOffset CreatedAt);
public sealed record FairnessResult(long Id, string GameId, string AlgorithmVersion,
    string Commitment, string CanonicalInputHash, string RevealedSeed, string ResultHash,
    int Result, FairnessEntropySource EntropySource, FairnessAuditStatus Status,
    DateTimeOffset CreatedAt, DateTimeOffset CompletedAt);
public sealed record FairnessVerification(bool Valid, string? Error);

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
