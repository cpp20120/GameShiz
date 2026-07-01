namespace BotFramework.Host.Admin.Audit;

public interface IAdminAuditReader
{
    Task<IReadOnlyList<AdminAuditRow>> ListAsync(
        int limit,
        string? actor,
        string? action,
        string? details,
        DateTimeOffset? from,
        DateTimeOffset? until,
        CancellationToken ct);
}
