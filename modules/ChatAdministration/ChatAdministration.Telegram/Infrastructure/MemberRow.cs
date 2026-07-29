namespace ChatAdministration.Telegram.Infrastructure;

internal sealed record MemberRow(
    long ChatId,
    long UserId,
    string? Username,
    string? DisplayName,
    string? Status,
    string? RolesJson,
    string? CustomRolesJson,
    string? ExplicitPermissionsJson,
    string? TrustLevel,
    string? DesiredRestrictionJson,
    string? ObservedRestrictionJson,
    DateTime FirstSeenAt,
    DateTime LastSeenAt,
    DateTime? JoinedAt,
    DateTime? LeftAt);
