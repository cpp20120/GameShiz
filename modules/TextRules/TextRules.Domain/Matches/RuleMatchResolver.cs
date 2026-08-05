using TextRules.Domain.Rules;

namespace TextRules.Domain.Matches;

public static class RuleMatchResolver
{
    public static RuleMatchResult Resolve(
        IReadOnlyList<RuleMatch> matches,
        IReadOnlyList<RuleMatchDiagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(matches);

        var precedenceOrdered = matches
            .OrderByDescending(match => match.Scope.Specificity)
            .ThenByDescending(match => match.Priority)
            .ThenByDescending(match => DispositionPrecedence(match.Disposition))
            .ThenByDescending(match => match.PatternLength > 0
                ? match.PatternLength
                : match.CanonicalSpan.Length)
            .ThenBy(match => match.RuleId.Value, StringComparer.Ordinal)
            .ThenBy(match => match.CanonicalSpan.Start)
            .ToArray();

        var effective = new List<RuleMatch>(precedenceOrdered.Length);
        foreach (var candidate in precedenceOrdered)
        {
            if (candidate.Disposition == RuleDisposition.Observe)
            {
                effective.Add(candidate);
                continue;
            }

            var overlapsSelectedDecision = effective.Any(
                selected => selected.Disposition != RuleDisposition.Observe
                    && selected.CanonicalSpan.Intersects(candidate.CanonicalSpan));
            if (!overlapsSelectedDecision)
                effective.Add(candidate);
        }

        return new RuleMatchResult(OrderForPresentation(matches), OrderForPresentation(effective), diagnostics);
    }

    private static RuleMatch[] OrderForPresentation(IEnumerable<RuleMatch> matches) => matches
        .OrderBy(match => match.OriginalSpan.Start)
        .ThenByDescending(match => match.OriginalSpan.Length)
        .ThenByDescending(match => match.Scope.Specificity)
        .ThenByDescending(match => match.Priority)
        .ThenByDescending(match => DispositionPrecedence(match.Disposition))
        .ThenBy(match => match.RuleId.Value, StringComparer.Ordinal)
        .ThenBy(match => match.CanonicalSpan.Start)
        .ToArray();

    private static int DispositionPrecedence(RuleDisposition disposition) => disposition switch
    {
        RuleDisposition.Allow => 3,
        RuleDisposition.Deny => 2,
        RuleDisposition.Observe => 1,
        _ => 0,
    };
}
