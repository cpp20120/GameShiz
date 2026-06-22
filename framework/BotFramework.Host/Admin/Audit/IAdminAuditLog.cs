namespace BotFramework.Host.Admin;

public interface IAdminAuditLog
{
    Task LogAsync(long actorId, string actorName, string action, object? details = null, CancellationToken ct = default);
}
