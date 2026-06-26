using BotFramework.Host.Contracts.Telegram;
using Dapper;
using Telegram.Bot.Types.Enums;

namespace BotFramework.Host.TelegramOutbox;

public sealed class PostgresTelegramOutboxStore(INpgsqlConnectionFactory connections) : ITelegramOutboxStore
{
    private const int MaxTextLength = 4096;
    private const int MaxErrorLength = 8000;

    public async Task EnqueueAsync(TelegramOutboxMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.ChatId == 0) throw new ArgumentException("ChatId must be non-zero.", nameof(message));
        if (string.IsNullOrWhiteSpace(message.Text)) throw new ArgumentException("Text is required.", nameof(message));

        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO telegram_outbox (
                dedupe_key,
                chat_id,
                text,
                parse_mode,
                status,
                next_attempt_at,
                created_at,
                updated_at
            )
            VALUES (
                @dedupeKey,
                @chatId,
                @text,
                @parseMode,
                'pending',
                now(),
                now(),
                now()
            )
            ON CONFLICT (dedupe_key) WHERE dedupe_key IS NOT NULL DO NOTHING
            """,
            new
            {
                dedupeKey = NullIfWhiteSpace(message.DedupeKey),
                chatId = message.ChatId,
                text = Truncate(message.Text, MaxTextLength),
                parseMode = ToDb(message.ParseMode),
            },
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<TelegramOutboxRow>> ClaimDueAsync(int limit, TimeSpan lease, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var rows = await conn.QueryAsync<TelegramOutboxDbRow>(new CommandDefinition(
            """
            WITH due AS (
                SELECT id
                FROM telegram_outbox
                WHERE status = 'pending'
                  AND next_attempt_at <= now()
                  AND (locked_until IS NULL OR locked_until <= now())
                ORDER BY next_attempt_at, id
                LIMIT @limit
                FOR UPDATE SKIP LOCKED
            )
            UPDATE telegram_outbox o
            SET status = 'sending',
                attempts = o.attempts + 1,
                locked_until = now() + (@leaseMs * interval '1 millisecond'),
                updated_at = now()
            FROM due
            WHERE o.id = due.id
            RETURNING
                o.id AS Id,
                o.chat_id AS ChatId,
                o.text AS Text,
                o.parse_mode AS ParseMode,
                o.attempts AS Attempts
            """,
            new { limit = Math.Clamp(limit, 1, 100), leaseMs = Math.Max(lease.TotalMilliseconds, 1) },
            transaction: tx,
            cancellationToken: ct));

        await tx.CommitAsync(ct);
        return rows
            .Select(row => new TelegramOutboxRow(row.Id, row.ChatId, row.Text, FromDb(row.ParseMode), row.Attempts))
            .ToList();
    }

    public async Task MarkSentAsync(long id, int telegramMessageId, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE telegram_outbox
            SET status = 'sent',
                telegram_message_id = @telegramMessageId,
                sent_at = now(),
                locked_until = NULL,
                updated_at = now()
            WHERE id = @id
            """,
            new { id, telegramMessageId },
            cancellationToken: ct));
    }

    public async Task MarkFailedAsync(long id, string errorMessage, TimeSpan retryAfter, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE telegram_outbox
            SET status = 'pending',
                last_error = @error,
                next_attempt_at = now() + (@retryAfterMs * interval '1 millisecond'),
                locked_until = NULL,
                updated_at = now()
            WHERE id = @id
            """,
            new
            {
                id,
                error = Truncate(errorMessage, MaxErrorLength),
                retryAfterMs = Math.Max(retryAfter.TotalMilliseconds, 1),
            },
            cancellationToken: ct));
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string ToDb(ParseMode parseMode) =>
        parseMode == ParseMode.None ? "" : parseMode.ToString();

    private static ParseMode FromDb(string? parseMode)
    {
        if (string.IsNullOrWhiteSpace(parseMode)) return ParseMode.None;

        return Enum.TryParse<ParseMode>(parseMode, ignoreCase: true, out var parsed)
            ? parsed
            : ParseMode.None;
    }

    private sealed record TelegramOutboxDbRow(
        long Id,
        long ChatId,
        string Text,
        string? ParseMode,
        int Attempts);
}
