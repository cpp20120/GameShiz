namespace TextRules.Domain.Rules;

public sealed record TextRule
{
    public required TextRuleId Id { get; init; }
    public required string Pattern { get; init; }
    public required TextRuleKind Kind { get; init; }
    public required RuleDisposition Disposition { get; init; }
    public required RuleScope Scope { get; init; }
    public string? Category { get; init; }
    public string? Reason { get; init; }
    public int Priority { get; init; } = 100;
    public bool Enabled { get; init; } = true;
    public TextRuleOptions Options { get; init; } = new();
}
