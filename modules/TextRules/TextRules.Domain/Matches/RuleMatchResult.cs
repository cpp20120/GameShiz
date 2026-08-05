using System.Collections.Immutable;

namespace TextRules.Domain.Matches;

public sealed record RuleMatchResult
{
    public RuleMatchResult(
        IReadOnlyList<RuleMatch> matches,
        IReadOnlyList<RuleMatch> effectiveMatches,
        IReadOnlyList<RuleMatchDiagnostic>? diagnostics = null)
    {
        Matches = matches?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(matches));
        EffectiveMatches = effectiveMatches?.ToImmutableArray()
            ?? throw new ArgumentNullException(nameof(effectiveMatches));
        Diagnostics = diagnostics?.ToImmutableArray() ?? [];
    }

    public IReadOnlyList<RuleMatch> Matches { get; }
    public IReadOnlyList<RuleMatch> EffectiveMatches { get; }
    public IReadOnlyList<RuleMatchDiagnostic> Diagnostics { get; }
}
