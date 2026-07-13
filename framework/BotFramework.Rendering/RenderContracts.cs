using System.Runtime.CompilerServices;

namespace BotFramework.Rendering;

public enum RenderPriority
{
    Interactive = 0,
    Background = 1,
    Prewarm = 2,
}

public sealed record RenderKey(
    string RendererId,
    string RendererVersion,
    string ContentHash,
    string Extension,
    string ContentType)
{
    public string Value => $"{RendererId}:{RendererVersion}:{ContentHash}";

    public string ObjectName =>
        $"artifacts/{Segment(RendererId)}/{Segment(RendererVersion)}/{Segment(ContentHash)}.{Segment(Extension).TrimStart('.')}";

    private static string Segment(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return string.Concat(value.Select(static ch =>
            char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '-'));
    }
}

public sealed record RenderOutput(byte[] Content, string? FileName = null)
{
    public static RenderOutput FromBytes(byte[] content, string? fileName = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new RenderOutput(content, fileName);
    }
}

public sealed record RenderedArtifact(
    RenderKey Key,
    byte[] Content,
    string FileName,
    DateTimeOffset CreatedAt,
    string StoreObjectName,
    bool CacheHit);

public interface IRenderJob<in TSpec>
{
    RenderKey Describe(TSpec spec);

    ValueTask<RenderOutput> RenderAsync(TSpec spec, CancellationToken ct);
}

public interface IRenderQueue
{
    ValueTask<RenderedArtifact> GetOrRenderAsync<TSpec>(
        TSpec spec,
        RenderPriority priority = RenderPriority.Interactive,
        CancellationToken ct = default);

    Task PrewarmAsync<TSpec>(IEnumerable<TSpec> specs, CancellationToken ct = default);
}

public sealed record RenderHistoryEntry(
    string GameId,
    string AggregateId,
    string MatchId,
    RenderKey ArtifactKey,
    DateTimeOffset CreatedAt,
    IReadOnlyDictionary<string, string>? Metadata = null);

public interface IRenderHistory
{
    ValueTask RecordAsync(RenderHistoryEntry entry, CancellationToken ct = default);

    IAsyncEnumerable<RenderHistoryEntry> ListAsync(
        string gameId,
        string aggregateId,
        int take = 50,
        CancellationToken ct = default);
}

public interface IRenderArtifactStore
{
    ValueTask<RenderedArtifact?> FindAsync(RenderKey key, CancellationToken ct);

    ValueTask<RenderedArtifact> PutAsync(RenderKey key, RenderOutput output, CancellationToken ct);

    ValueTask RecordHistoryAsync(RenderHistoryEntry entry, CancellationToken ct);

    IAsyncEnumerable<RenderHistoryEntry> ListHistoryAsync(
        string gameId,
        string aggregateId,
        int take,
        CancellationToken ct);
}
