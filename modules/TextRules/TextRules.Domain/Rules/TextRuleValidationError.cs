namespace TextRules.Domain.Rules;

public sealed record TextRuleValidationError(
    TextRuleId? RuleId,
    string Code,
    string Message);
