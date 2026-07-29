using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed class ForbiddenWordsRule(RuleId id, ForbiddenWordsPolicy policy) : IModerationRule
{
    public RuleId Id { get; } = id;

    public Violation? Evaluate(ModerationMessageContext context)
    {
        var text = context.Message.Text ?? string.Empty;
        var comparison = policy.CaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var word = policy.Words.FirstOrDefault(value => text.Contains(value, comparison));
        if (word is null)
            return null;

        return new Violation
        {
            RuleId = Id,
            Code = "forbidden_word",
            Score = 10,
            Severity = ViolationSeverity.Critical,
            Reason = "Обнаружено запрещённое слово.",
            Metadata = new Dictionary<string, object?> { ["matched"] = word },
        };
    }
}
