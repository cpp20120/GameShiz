namespace BotFramework.Rendering;

public sealed record RenderHistoryEntry(
    string GameId,
    string AggregateId,
    string MatchId,
    RenderKey ArtifactKey,
    DateTimeOffset CreatedAt,
    IReadOnlyDictionary<string, string>? Metadata = null);
