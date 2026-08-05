namespace BotFramework.Text;

/// <summary>
/// A generic match. Pattern contains the consumer-owned pattern instance.
/// </summary>
public sealed record Match
{
    public required object Pattern { get; init; }
    public double Confidence { get; init; } = 1d;
    public required TextSpan Span { get; init; }
    public IReadOnlySet<MatchSignal> Signals { get; init; } = new HashSet<MatchSignal>();

    public TextSpan? OriginalSpan(NormalizedText text) => text.MapToOriginal(Span);
}
