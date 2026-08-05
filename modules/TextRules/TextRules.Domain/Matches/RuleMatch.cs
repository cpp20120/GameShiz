using BotFramework.Text;
using TextRules.Domain.Rules;

namespace TextRules.Domain.Matches;

public sealed record RuleMatch
{
    public required TextRuleId RuleId { get; init; }
    public required RuleDisposition Disposition { get; init; }
    public required RuleScope Scope { get; init; }
    public required int Priority { get; init; }
    /// <summary>
    /// Canonical length of the rule pattern used for deterministic tie-breaking.
    /// </summary>
    public int PatternLength { get; init; }
    public string? Category { get; init; }
    public string? Reason { get; init; }
    public required TextSpan CanonicalSpan { get; init; }
    public required TextSpan OriginalSpan { get; init; }
    public required RuleMatchKind MatchKind { get; init; }
    public required double Confidence { get; init; }
}
