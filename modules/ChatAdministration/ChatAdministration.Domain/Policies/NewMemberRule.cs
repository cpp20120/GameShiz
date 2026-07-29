using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed class NewMemberRule(RuleId id, NewMemberPolicy policy) : IModerationRule
{
    public RuleId Id { get; } = id;

    public Violation? Evaluate(ModerationMessageContext context)
    {
        if (!policy.Enabled || policy.Window <= TimeSpan.Zero || context.Author.FirstSeenAt == default)
            return null;
        if (context.Message.SentAt > context.Author.FirstSeenAt.Add(policy.Window))
            return null;

        return new Violation
        {
            RuleId = Id,
            Code = "new_member",
            Score = Math.Max(1, policy.Score),
            Severity = ViolationSeverity.Low,
            Reason = "Для нового участника действует усиленная политика.",
        };
    }
}
