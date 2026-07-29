namespace ChatAdministration.Domain.Models;

public sealed record CustomRoleDefinition
{
    public required RoleId Id { get; init; }
    public required string DisplayName { get; init; }
    public int Rank { get; init; }
    public IReadOnlySet<Permission> Permissions { get; init; } = new HashSet<Permission>();
}
