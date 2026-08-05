namespace TextRules.Domain.Rules;

public sealed record RuleSet
{
    public required long Version { get; init; }
    public required IReadOnlyList<TextRule> Rules { get; init; }
}
