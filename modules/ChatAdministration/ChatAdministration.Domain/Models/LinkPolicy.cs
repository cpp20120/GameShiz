namespace ChatAdministration.Domain.Models;

public sealed record LinkPolicy
{
    public LinkPolicyMode Mode { get; init; } = LinkPolicyMode.AllowAll;
    public IReadOnlySet<string> AllowedDomains { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
