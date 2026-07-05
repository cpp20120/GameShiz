using BotFramework.Host.Contracts.Telegram;
using Dapper;

namespace BotFramework.Host.TelegramOutbox;

public sealed class PostgresTelegramOutboxStore(INpgsqlConnectionFactory connections) : ITelegramOutboxStore
{
    private const int MaxTextLength = 4096;
    private const int MaxErrorLength = 8000;
    private const int PreviewLength = 240;

    public async Task<IReadOnlyList<TelegramOutboxAdminRow>> ListUnsentAsync(
        int limit,
        string? status,
        CancellationToken ct)
    {
        var normalizedStatus = status is "pending" or "sending" ? status : null;
        await using var conn = await connections.OpenAsync(ct);
        var rows = await conn.QueryAsync<TelegramOutboxAdminRow>(new CommandDefinition(
            """
            SELECT id AS Id,
                   chat_id AS ChatId,
                   status AS Status,
                   attempts AS Attempts,
                   next_attempt_at AS NextAttemptAt,
                   locked_until AS LockedUntil,
                   last_error AS LastError,
                   left(text, @previewLength) AS MessagePreview,
                   created_at AS CreatedAt,
                   updated_at AS UpdatedAt
            FROM telegram_outbox
            WHERE status IN ('pending', 'sending')
              AND (@status IS NULL OR status = @status)
            ORDER BY created_at DESC, id DESC
            LIMIT @limit
            """,
            new
            {
                limit = Math.Clamp(limit, 1, 100),
                status = normalizedStatus,
                previewLength = PreviewLength,
            },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<TelegramOutboxRescheduleResult> RescheduleNowAsync(long id, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var row = await conn.QuerySingleOrDefaultAsync<RescheduleState>(new CommandDefinition(
            """
            SELECT status AS Status, locked_until AS LockedUntil
            FROM telegram_outbox
            WHERE id = @id
            FOR UPDATE
            """,
            new { id },
            transaction: tx,
            cancellationToken: ct));

        TelegramOutboxRescheduleResult result;
        if (row is null)
        {
            result = new(TelegramOutboxRescheduleOutcome.NotFound, "outbox record not found");
        }
        else if (TelegramOutboxReschedulePolicy.Classify(row.Status, row.LockedUntil, DateTimeOffset.UtcNow)
                 is var outcome and not TelegramOutboxRescheduleOutcome.Rescheduled)
        {
            var message = outcome == TelegramOutboxRescheduleOutcome.ActivelySending
                ? "outbox record is currently leased by the dispatcher"
                : $"outbox record has terminal status '{row.Status}'";
            result = new(outcome, message);
        }
        else
        {
            await conn.ExecuteAsync(new CommandDefinition(
                """
                UPDATE telegram_outbox
                SET status = 'pending',
                    next_attempt_at = now(),
                    locked_until = NULL,
                    updated_at = now()
                WHERE id = @id
                """,
                new { id },
                transaction: tx,
                cancellationToken: ct));
            result = new(TelegramOutboxRescheduleOutcome.Rescheduled, "outbox record rescheduled for immediate delivery");
        }

        await tx.CommitAsync(ct);
        return result;
    }

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

    private static string ToDb(OutboundParseMode parseMode) =>
        parseMode == OutboundParseMode.None ? "" : parseMode.ToString();

    private static OutboundParseMode FromDb(string? parseMode)
    {
        if (string.IsNullOrWhiteSpace(parseMode)) return OutboundParseMode.None;

        return Enum.TryParse<OutboundParseMode>(parseMode, ignoreCase: true, out var parsed)
            ? parsed
            : OutboundParseMode.None;
    }

    private sealed record TelegramOutboxDbRow(
        long Id,
        long ChatId,
        string Text,
        string? ParseMode,
        int Attempts);

    private sealed record RescheduleState(string Status, DateTimeOffset? LockedUntil);
}
