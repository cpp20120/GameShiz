namespace BotFramework.Text;

public sealed record AnalysisResult
{
    public required string AnalyzerId { get; init; }
    public IReadOnlyList<Match> Matches { get; init; } = [];
    public IReadOnlySet<TextSignal> Signals { get; init; } = new HashSet<TextSignal>();
    public IReadOnlyDictionary<string, object?> Values { get; init; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);
}
