using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public interface IModerationRule
{
    RuleId Id { get; }
    Violation? Evaluate(ModerationMessageContext context);
}
