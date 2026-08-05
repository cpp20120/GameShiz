namespace BotFramework.Text;

/// <summary>
/// A generic match. Pattern contains the consumer-owned pattern instance.
/// </summary>
public sealed record Match
{
    private double _confidence = 1d;

    public required object Pattern { get; init; }

    public double Confidence
    {
        get => _confidence;
        init
        {
            if (double.IsNaN(value) || value is < 0d or > 1d)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Confidence must be between 0 and 1.");
            _confidence = value;
        }
    }

    public required TextSpan Span { get; init; }
    public IReadOnlySet<MatchSignal> Signals { get; init; } = new HashSet<MatchSignal>();

    public TextSpan? OriginalSpan(NormalizedText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.MapToOriginal(Span);
    }

    public string CanonicalFragment(NormalizedText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Span.Slice(text.CanonicalText);
    }

    public string? OriginalFragment(NormalizedText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return OriginalSpan(text)?.Slice(text.OriginalText);
    }
}
