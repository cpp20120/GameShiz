namespace Games.Pick.Application.Services;

public interface IPickService
{
    Task<PickResult> PickAsync(
        long userId,
        string displayName,
        long chatId,
        int amount,
        IReadOnlyList<string> variants,
        IReadOnlyList<int> backedIndices,
        CancellationToken ct);

    Task<PickResult> PickAsync(
        long userId,
        string displayName,
        long chatId,
        int amount,
        IReadOnlyList<string> variants,
        IReadOnlyList<int> backedIndices,
        int sourceMessageId,
        CancellationToken ct);

    /// <summary>Continue an existing chain. Caller has already claimed the chain state.</summary>
    Task<PickResult> ContinueChainAsync(PickChainState chain, CancellationToken ct);
    Task<PickChainState?> ClaimChainAsync(Guid chainId, CancellationToken ct);
    Task RestoreChainAsync(PickChainState chain, CancellationToken ct);
}
