using System.Text.RegularExpressions;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed class LinkRule(RuleId id, LinkPolicy policy) : IModerationRule
{
    private static readonly Regex UrlRegex = new(
        @"\b(?:https?://|www\.)\S+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public RuleId Id { get; } = id;

    public Violation? Evaluate(ModerationMessageContext context)
    {
        if (!HasLink(context.Message))
            return null;
        if (policy.Mode == LinkPolicyMode.AllowAll)
            return null;
        if (policy.Mode == LinkPolicyMode.AllowTrusted && context.Author.Roles.Contains(ChatMemberRole.Trusted))
            return null;

        return new Violation
        {
            RuleId = Id,
            Code = "link_policy",
            Score = 7,
            Severity = ViolationSeverity.High,
            Reason = "Ссылки запрещены политикой чата.",
        };
    }

    private static bool HasLink(NormalizedMessage message) =>
        message.Entities.Any(entity => entity.Type is MessageEntityType.Url or MessageEntityType.TextLink)
        || UrlRegex.IsMatch(message.Text ?? string.Empty);
}
