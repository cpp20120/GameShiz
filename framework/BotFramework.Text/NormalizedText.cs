namespace BotFramework.Text;

public sealed record NormalizedText
{
    public required string OriginalText { get; init; }
    public required string CanonicalText { get; init; }
    public required IReadOnlyList<Token> Tokens { get; init; }
    public required IReadOnlyList<TextSpanMapping> Spans { get; init; }
    public required IReadOnlySet<TextSignal> Signals { get; init; }

    /// <summary>
    /// Maps a canonical span to the smallest source span covering all contributing text.
    /// </summary>
    public TextSpan? MapToOriginal(TextSpan canonicalSpan)
    {
        if (canonicalSpan.End > CanonicalText.Length)
            throw new ArgumentOutOfRangeException(nameof(canonicalSpan));
        if (canonicalSpan.IsEmpty)
            return new TextSpan(canonicalSpan.Start, 0);

        var mappings = Spans
            .Where(mapping => mapping.Canonical.Intersects(canonicalSpan))
            .ToArray();
        if (mappings.Length == 0)
            return null;

        var start = mappings.Min(mapping => mapping.Original.Start);
        var end = mappings.Max(mapping => mapping.Original.End);
        return new TextSpan(start, end - start);
    }
}
