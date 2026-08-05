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
            return MapEmptySpan(canonicalSpan.Start);

        var mappings = Spans
            .Where(mapping => mapping.Canonical.Intersects(canonicalSpan))
            .ToArray();
        if (mappings.Length == 0)
            return null;

        var result = mappings[0].Original;
        for (var index = 1; index < mappings.Length; index++)
            result = result.Union(mappings[index].Original);
        return result;
    }

    private TextSpan MapEmptySpan(int canonicalPosition)
    {
        if (canonicalPosition == 0)
            return new TextSpan(0, 0);

        var preceding = Spans
            .Where(mapping => mapping.Canonical.End <= canonicalPosition)
            .OrderByDescending(mapping => mapping.Canonical.End)
            .FirstOrDefault();
        if (preceding != default)
            return new TextSpan(preceding.Original.End, 0);

        var following = Spans
            .Where(mapping => mapping.Canonical.Start >= canonicalPosition)
            .OrderBy(mapping => mapping.Canonical.Start)
            .FirstOrDefault();
        return following == default
            ? new TextSpan(OriginalText.Length, 0)
            : new TextSpan(following.Original.Start, 0);
    }
}
