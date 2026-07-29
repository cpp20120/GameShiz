using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed class ScoreOverrideModerationRule(
    IModerationRule inner,
    int score) : IModerationRule
{
    public RuleId Id => inner.Id;

    public Violation? Evaluate(ModerationMessageContext context)
    {
        var violation = inner.Evaluate(context);
        return violation is null ? null : violation with { Score = Math.Max(1, score) };
    }
}
