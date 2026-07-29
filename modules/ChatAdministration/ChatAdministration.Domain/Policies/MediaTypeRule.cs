using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed class MediaTypeRule(RuleId id, MediaTypePolicy policy) : IModerationRule
{
    public RuleId Id { get; } = id;

    public Violation? Evaluate(ModerationMessageContext context)
    {
        if (!policy.BlockedTypes.Contains(context.Message.ContentType))
            return null;

        return new Violation
        {
            RuleId = Id,
            Code = "media_type",
            Score = Math.Max(1, policy.Score),
            Severity = ViolationSeverity.Medium,
            Reason = $"Тип сообщения запрещён: {context.Message.ContentType}.",
            Metadata = new Dictionary<string, object?> { ["content_type"] = context.Message.ContentType.ToString() },
        };
    }
}
