namespace ChatAdministration.Domain.Models;

public sealed record MemberState
{
    public required ChatId ChatId { get; init; }
    public required UserId UserId { get; init; }
    public string? Username { get; init; }
    public string DisplayName { get; init; } = "unknown";
    public MemberStatus Status { get; init; } = MemberStatus.Active;
    public IReadOnlySet<ChatMemberRole> Roles { get; init; } = new HashSet<ChatMemberRole>();
    public IReadOnlySet<Permission> ExplicitPermissions { get; init; } = new HashSet<Permission>();
    public IReadOnlySet<RoleId> CustomRoleIds { get; init; } = new HashSet<RoleId>();
    public int ActiveWarningCount { get; init; }
    public RestrictionState? DesiredRestriction { get; init; }
    public RestrictionState? ObservedRestriction { get; init; }
    public DateTimeOffset FirstSeenAt { get; init; }
    public DateTimeOffset LastSeenAt { get; init; }
    public DateTimeOffset? JoinedAt { get; init; }
    public DateTimeOffset? LeftAt { get; init; }
    public TrustLevel TrustLevel { get; init; } = TrustLevel.Unknown;
}
