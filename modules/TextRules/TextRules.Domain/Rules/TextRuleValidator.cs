namespace TextRules.Domain.Rules;

public static class TextRuleValidator
{
    public static IReadOnlyList<TextRuleValidationError> Validate(RuleSet ruleSet)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);

        var errors = new List<TextRuleValidationError>();
        if (ruleSet.Version <= 0)
        {
            errors.Add(new TextRuleValidationError(
                null,
                "version",
                $"Rule-set version must be positive, but was {ruleSet.Version}."));
        }

        if (ruleSet.Rules is null)
        {
            errors.Add(new TextRuleValidationError(null, "rules", "Rule-set rules are required."));
            return errors;
        }

        var ids = new HashSet<TextRuleId>();
        foreach (var rule in ruleSet.Rules)
            errors.AddRange(ValidateRule(rule, ids));

        return errors;
    }

    public static void EnsureValid(RuleSet ruleSet)
    {
        var errors = Validate(ruleSet);
        if (errors.Count > 0)
            throw new TextRuleValidationException(errors);
    }

    private static string FormatId(TextRuleId id) => id.IsEmpty ? "<empty>" : id.Value;

    private static List<TextRuleValidationError> ValidateRule(
        TextRule? rule,
        HashSet<TextRuleId> ids)
    {
        if (rule is null)
            return [new TextRuleValidationError(null, "rule", "Rule definitions cannot contain null.")];

        var errors = new List<TextRuleValidationError>();
        TextRuleId? ruleId = rule.Id.IsEmpty ? null : rule.Id;
        if (!rule.Id.IsEmpty && !ids.Add(rule.Id))
        {
            errors.Add(new TextRuleValidationError(
                rule.Id,
                "duplicate_id",
                $"Rule id '{rule.Id}' is duplicated."));
        }
        if (rule.Id.IsEmpty)
            errors.Add(new TextRuleValidationError(ruleId, "id", "Rule id must be non-empty."));
        if (string.IsNullOrWhiteSpace(rule.Pattern))
        {
            errors.Add(new TextRuleValidationError(
                ruleId,
                "pattern",
                $"Rule '{FormatId(rule.Id)}' must have a non-empty pattern."));
        }
        if (!Enum.IsDefined(rule.Kind))
        {
            errors.Add(new TextRuleValidationError(
                ruleId,
                "kind",
                $"Rule '{FormatId(rule.Id)}' has unsupported kind '{rule.Kind}'."));
        }
        if (!Enum.IsDefined(rule.Disposition))
        {
            errors.Add(new TextRuleValidationError(
                ruleId,
                "disposition",
                $"Rule '{FormatId(rule.Id)}' has unsupported disposition '{rule.Disposition}'."));
        }
        if (rule.Scope is null || !rule.Scope.IsValid)
        {
            errors.Add(new TextRuleValidationError(
                ruleId,
                "scope",
                $"Rule '{FormatId(rule.Id)}' has an invalid scope."));
        }
        if (rule.Options is null)
        {
            errors.Add(new TextRuleValidationError(
                ruleId,
                "options",
                $"Rule '{FormatId(rule.Id)}' must have rule options."));
        }
        else if (rule.Kind is TextRuleKind.Phrase or TextRuleKind.Regex
                 && !rule.Options.MatchWholeToken)
        {
            errors.Add(new TextRuleValidationError(
                ruleId,
                "options",
                $"Rule '{FormatId(rule.Id)}' uses MatchWholeToken=false, which is supported only for token rules."));
        }

        return errors;
    }
}
