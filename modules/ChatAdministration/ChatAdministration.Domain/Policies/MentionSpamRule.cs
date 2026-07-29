using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed class MentionSpamRule(RuleId id, MentionSpamPolicy policy) : IModerationRule
{
    public RuleId Id { get; } = id;

    public Violation? Evaluate(ModerationMessageContext context)
    {
        var mentions = context.Message.Entities.Count(entity => entity.Type is MessageEntityType.Mention or MessageEntityType.TextMention);
        if (!policy.Enabled)
            return null;
        if (policy.MaximumMentions <= 0)
            return null;
        if (mentions <= policy.MaximumMentions)
            return null;

        return new Violation
        {
            RuleId = Id,
            Code = "mention_spam",
            Score = 7,
            Severity = ViolationSeverity.High,
            Reason = $"Слишком много упоминаний: {mentions}.",
            Metadata = new Dictionary<string, object?> { ["mentions"] = mentions },
        };
    }
}
