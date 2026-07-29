using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed class CapsRule(RuleId id, double minimumUppercaseRatio = 0.7, int minimumLetters = 8) : IModerationRule
{
    public RuleId Id { get; } = id;

    public Violation? Evaluate(ModerationMessageContext context)
    {
        var letters = (context.Message.Text ?? string.Empty).Where(char.IsLetter).ToArray();
        if (letters.Length < minimumLetters)
            return null;
        var uppercaseRatio = letters.Count(char.IsUpper) / (double)letters.Length;
        if (uppercaseRatio < minimumUppercaseRatio)
            return null;

        return new Violation
        {
            RuleId = Id,
            Code = "caps",
            Score = 4,
            Severity = ViolationSeverity.Low,
            Reason = "Слишком большая доля заглавных букв.",
            Metadata = new Dictionary<string, object?> { ["uppercase_ratio"] = uppercaseRatio },
        };
    }
}
