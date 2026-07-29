namespace ChatAdministration.Domain.Models;

public enum ModerationCaseStatus
{
    Requested,
    Applying,
    Applied,
    PartiallyApplied,
    Failed,
    Unknown,
    Revoking,
    RevocationUnknown,
    Expired,
    Revoked,
    Compensated,
}
