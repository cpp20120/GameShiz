using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;

namespace ChatAdministration.Application.Services;

public interface IModerationRuleProvider
{
    IReadOnlyCollection<IModerationRule> GetRules(ChatState chat);
}
