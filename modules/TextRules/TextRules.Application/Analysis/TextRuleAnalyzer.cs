using System.Collections.Immutable;
using BotFramework.Text;
using TextRules.Application.Matching;
using TextRules.Application.Sources;
using TextRules.Domain.Matches;

namespace TextRules.Application.Analysis;

public sealed class TextRuleAnalyzer(
    ITextRuleSnapshotProvider snapshots,
    ITextRuleMatcher matcher,
    ITextRuleScopeResolver scopeResolver) : ITextAnalyzer
{
    private readonly ITextRuleSnapshotProvider _snapshots =
        snapshots ?? throw new ArgumentNullException(nameof(snapshots));
    private readonly ITextRuleMatcher _matcher =
        matcher ?? throw new ArgumentNullException(nameof(matcher));
    private readonly ITextRuleScopeResolver _scopeResolver =
        scopeResolver ?? throw new ArgumentNullException(nameof(scopeResolver));

    public string Name => "text-rules";
    public int Order => 1000;

    public async ValueTask<AnalysisResult> AnalyzeAsync(
        TextAnalysisContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var scope = _scopeResolver.Resolve(context.ProcessingContext);
        var snapshot = await _snapshots.GetAsync(scope, cancellationToken);
        var result = _matcher.Match(context.Text, snapshot);
        var effective = result.EffectiveMatches.ToHashSet();
        var facts = result.Matches
            .Select(match => new TextRuleMatchedFact
            {
                Match = match,
                IsEffective = effective.Contains(match),
            })
            .ToImmutableArray();
        var genericMatches = result.Matches
            .Select(match => new Match
            {
                Pattern = match.RuleId,
                Confidence = match.Confidence,
                Span = match.CanonicalSpan,
            })
            .ToImmutableArray();

        return new AnalysisResult
        {
            AnalyzerId = Name,
            Matches = genericMatches,
            Values = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [TextRuleFacts.MatchesKey] = facts,
                [TextRuleFacts.DiagnosticsKey] = result.Diagnostics,
            },
        };
    }
}
