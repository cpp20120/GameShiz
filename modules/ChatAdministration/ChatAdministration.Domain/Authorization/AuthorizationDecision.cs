namespace ChatAdministration.Domain.Authorization;

public sealed record AuthorizationDecision(bool Allowed, string? ErrorCode = null)
{
    public static AuthorizationDecision Allow() => new(true);
    public static AuthorizationDecision Deny(string errorCode) => new(false, errorCode);
}
