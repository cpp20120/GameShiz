using Dapper;

namespace BotFramework.Host.Admin.Audit;

public sealed class AdminAuditReader(INpgsqlConnectionFactory connections) : IAdminAuditReader
{
    public async Task<IReadOnlyList<AdminAuditRow>> ListAsync(
        int limit,
        string? actor,
        string? action,
        string? details,
        DateTimeOffset? from,
        DateTimeOffset? until,
        CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        var rows = await conn.QueryAsync<AdminAuditRow>(new CommandDefinition(
            """
            SELECT id AS Id,
                   actor_id AS ActorId,
                   actor_name AS ActorName,
                   action AS Action,
                   details::text AS DetailsJson,
                   occurred_at AS OccurredAt
            FROM admin_audit
            WHERE (@actor = '' OR actor_id::text = @actor OR actor_name ILIKE '%' || @actor || '%')
              AND (@action = '' OR action ILIKE '%' || @action || '%')
              AND (@details = '' OR details::text ILIKE '%' || @details || '%')
              AND (@from IS NULL OR occurred_at >= @from)
              AND (@until IS NULL OR occurred_at <= @until)
            ORDER BY occurred_at DESC, id DESC
            LIMIT @limit
            """,
            new
            {
                limit = Math.Clamp(limit, 1, 1_000),
                actor = actor?.Trim() ?? "",
                action = action?.Trim() ?? "",
                details = details?.Trim() ?? "",
                from,
                until,
            },
            cancellationToken: ct));
        return rows.ToList();
    }
}
