using BotFramework.Host.Contracts.Discord;
using Dapper;

namespace BotFramework.Host.DiscordOutbox;

public sealed class PostgresDiscordOutboxStore(INpgsqlConnectionFactory connections) : IDiscordOutboxStore
{
    private const int MaxTextLength = 4096;
    private const int MaxErrorLength = 8000;

    public async Task EnqueueAsync(DiscordOutboxMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.ChannelId == 0) throw new ArgumentException("ChannelId must be non-zero.", nameof(message));
        if (string.IsNullOrWhiteSpace(message.Text)) throw new ArgumentException("Text is required.", nameof(message));

        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO discord_outbox (
                dedupe_key, user_id, channel_id, text, title, culture,
                status, next_attempt_at, created_at, updated_at)
            VALUES (
                @dedupeKey, @userId, @channelId, @text, @title, @culture,
                'pending', now(), now(), now())
            ON CONFLICT (dedupe_key) WHERE dedupe_key IS NOT NULL DO NOTHING
            """,
            new
            {
                dedupeKey = NullIfWhiteSpace(message.DedupeKey),
                message.UserId,
                message.ChannelId,
                text = Truncate(message.Text, MaxTextLength),
                title = Truncate(message.Title, 256),
                culture = message.Culture,
            },
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<DiscordOutboxRow>> ClaimDueAsync(
        int limit, TimeSpan lease, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var rows = await conn.QueryAsync<DbRow>(new CommandDefinition(
            """
            WITH due AS (
                SELECT id
                FROM discord_outbox
                WHERE (status = 'pending'
                       AND next_attempt_at <= now()
                       AND (locked_until IS NULL OR locked_until <= now()))
                   OR (status = 'sending' AND locked_until <= now())
                ORDER BY next_attempt_at, id
                LIMIT @limit
                FOR UPDATE SKIP LOCKED
            )
            UPDATE discord_outbox o
            SET status = 'sending',
                attempts = o.attempts + 1,
                locked_until = now() + (@leaseMs * interval '1 millisecond'),
                updated_at = now()
            FROM due
            WHERE o.id = due.id
            RETURNING o.id AS Id, o.user_id AS UserId, o.channel_id AS ChannelId,
                      o.text AS Text, o.title AS Title, o.culture AS Culture,
                      o.attempts AS Attempts
            """,
            new { limit = Math.Clamp(limit, 1, 100), leaseMs = Math.Max(lease.TotalMilliseconds, 1) },
            transaction: tx,
            cancellationToken: ct));
        await tx.CommitAsync(ct);
        return rows.Select(row => new DiscordOutboxRow(
            row.Id, row.UserId, row.ChannelId, row.Text, row.Title, row.Culture, row.Attempts)).ToList();
    }

    public async Task MarkSentAsync(long id, long discordMessageId, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE discord_outbox
            SET status = 'sent', discord_message_id = @discordMessageId,
                sent_at = now(), locked_until = NULL, updated_at = now()
            WHERE id = @id
            """,
            new { id, discordMessageId }, cancellationToken: ct));
    }

    public async Task MarkFailedAsync(long id, string errorMessage, TimeSpan retryAfter, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE discord_outbox
            SET status = 'pending', last_error = @error,
                next_attempt_at = now() + (@retryAfterMs * interval '1 millisecond'),
                locked_until = NULL, updated_at = now()
            WHERE id = @id
            """,
            new
            {
                id,
                error = Truncate(errorMessage, MaxErrorLength),
                retryAfterMs = Math.Max(retryAfter.TotalMilliseconds, 1),
            }, cancellationToken: ct));
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? Truncate(string? value, int maxLength) =>
        value is null ? null : value.Length <= maxLength ? value : value[..maxLength];

    private sealed record DbRow(
        long Id,
        long UserId,
        long ChannelId,
        string Text,
        string? Title,
        string Culture,
        int Attempts);
}
