namespace BotFramework.Text;

/// <summary>
/// Business-neutral facts produced by one analyzer.
/// </summary>
public sealed record AnalysisResult
{
    public required string AnalyzerId { get; init; }
    public IReadOnlyList<Match> Matches { get; init; } = [];
    public IReadOnlySet<TextSignal> Signals { get; init; } = new HashSet<TextSignal>();
    public IReadOnlyDictionary<string, object?> Values { get; init; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    public bool HasMatches => Matches.Count > 0;
}
