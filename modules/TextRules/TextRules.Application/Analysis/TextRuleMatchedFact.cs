using TextRules.Domain.Matches;

namespace TextRules.Application.Analysis;

public sealed record TextRuleMatchedFact
{
    public required RuleMatch Match { get; init; }
    public required bool IsEffective { get; init; }
}
