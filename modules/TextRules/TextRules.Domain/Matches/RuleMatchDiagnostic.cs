using TextRules.Domain.Rules;

namespace TextRules.Domain.Matches;

public sealed record RuleMatchDiagnostic(
    TextRuleId RuleId,
    string Code,
    string Message);
