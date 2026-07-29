namespace ChatAdministration.Domain.Models;

public sealed record ForbiddenWordsPolicy
{
    public IReadOnlySet<string> Words { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public bool CaseInsensitive { get; init; } = true;
}
