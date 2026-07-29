namespace CasinoShiz.Host.Pages.Admin;

public sealed record ChatAdministrationMemberRow(
    long UserId,
    string? Username,
    string DisplayName,
    string Status,
    string RolesJson,
    string TrustLevel,
    string? DesiredRestrictionJson,
    string? ObservedRestrictionJson,
    DateTime LastSeenAt);
