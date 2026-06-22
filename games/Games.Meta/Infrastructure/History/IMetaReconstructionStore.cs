namespace Games.Meta;

public interface IMetaReconstructionStore
{
    Task<MetaReconstructionSummary> GetSummaryAsync(CancellationToken ct);
    Task<MetaReconstructionResult> ReconstructCoreAsync(CancellationToken ct);
}
