using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed class FloodRule(RuleId id, FloodPolicy policy) : IModerationRule
{
    public RuleId Id { get; } = id;

    public Violation? Evaluate(ModerationMessageContext context)
    {
        if (policy.MaximumMessages <= 0 || context.RateLimits.MessagesInWindow < policy.MaximumMessages)
            return null;

        return new Violation
        {
            RuleId = Id,
            Code = "flood",
            Score = 8,
            Severity = ViolationSeverity.High,
            Reason = $"Слишком много сообщений за {policy.Window.TotalSeconds:0} секунд.",
            Metadata = new Dictionary<string, object?>
            {
                ["messages"] = context.RateLimits.MessagesInWindow,
                ["window_seconds"] = policy.Window.TotalSeconds,
            },
        };
    }
}
