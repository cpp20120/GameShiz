namespace ChatAdministration.Domain.Models;

/// <summary>
/// Controls how long operational moderation data is retained for a chat.
/// </summary>
public sealed record RetentionPolicy
{
    public TimeSpan AuditLogRetention { get; init; } = TimeSpan.FromDays(365);
    public TimeSpan MessageIndexRetention { get; init; } = TimeSpan.FromDays(30);
    public TimeSpan CallbackStateRetention { get; init; } = TimeSpan.FromDays(1);
}
