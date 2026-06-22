namespace Games.Meta.Infrastructure.History;

public interface IMetaReconstructionStore
{
    Task<MetaReconstructionSummary> GetSummaryAsync(CancellationToken ct);
    Task<MetaReconstructionResult> ReconstructCoreAsync(CancellationToken ct);
}
