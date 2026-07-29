using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed class ForwardedMessageRule(RuleId id, ForwardedMessagePolicy policy) : IModerationRule
{
    public RuleId Id { get; } = id;

    public Violation? Evaluate(ModerationMessageContext context)
    {
        if (!policy.Enabled || !context.Message.IsForwarded)
            return null;

        return new Violation
        {
            RuleId = Id,
            Code = "forwarded_message",
            Score = Math.Max(1, policy.Score),
            Severity = ViolationSeverity.Medium,
            Reason = "Пересланные сообщения запрещены политикой чата.",
        };
    }
}
