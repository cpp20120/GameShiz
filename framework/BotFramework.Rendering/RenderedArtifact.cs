namespace BotFramework.Rendering;

public sealed record RenderedArtifact(
    RenderKey Key,
    byte[] Content,
    string FileName,
    DateTimeOffset CreatedAt,
    string StoreObjectName,
    bool CacheHit);
