namespace ChatAdministration.Domain.Models;

public sealed record RestrictionState
{
    public required bool CanSendMessages { get; init; }
    public DateTimeOffset? Until { get; init; }
}
