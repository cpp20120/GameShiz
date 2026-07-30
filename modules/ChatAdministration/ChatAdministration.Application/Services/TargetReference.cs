using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Services;

public sealed record TargetReference(
    UserId? UserId,
    string? Username,
    string? DisplayName,
    int? SourceMessageId = null)
{
    public static TargetReference ForUser(UserId userId, string? username = null, string? displayName = null) =>
        new(userId, username, displayName);

    public static TargetReference ForMessage(int messageId) =>
        new(null, null, null, messageId);
}
