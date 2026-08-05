using TextRules.Domain.Rules;

namespace TextRules.Application.Compilation;

public abstract record CompiledRule
{
    public required TextRuleId RuleId { get; init; }
    public required RuleDisposition Disposition { get; init; }
    public required RuleScope Scope { get; init; }
    public required int Priority { get; init; }
    public string? Category { get; init; }
    public string? Reason { get; init; }
    public required string Pattern { get; init; }
    public required int PatternLength { get; init; }
}
