using Dapper;
using BotFramework.Host.Composition.Builder;
using Microsoft.Extensions.Options;

namespace BotFramework.Telegram.Pipeline.Middleware;

public sealed partial class UpdateDeduplicationMiddleware(
    INpgsqlConnectionFactory connections,
    ILogger<UpdateDeduplicationMiddleware> logger,
    IOptions<BotFrameworkOptions> options) : IUpdateMiddleware
{
    private readonly string botId = string.IsNullOrWhiteSpace(options.Value.TenantKey)
        ? "default"
        : options.Value.TenantKey;

    public async Task InvokeAsync(UpdateContext ctx, UpdateDelegate next)
    {
        var updateId = ctx.Update.Id;
        if (updateId == 0)
        {
            await next(ctx);
            return;
        }

        if (!await TryBeginAsync(updateId, ctx.Ct))
        {
            LogDuplicate(updateId);
            return;
        }

        try
        {
            await next(ctx);
            await MarkCompletedAsync(updateId, ctx.Ct);
        }
        catch (Exception ex)
        {
            await MarkFailedAsync(updateId, ex);
            throw;
        }
    }

    private async Task<bool> TryBeginAsync(long updateId, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM processed_update_inbox
            WHERE bot_id = @botId AND update_id = @updateId
              AND status = 'processing'
              AND started_at < now() - interval '10 minutes'
            """,
            new { botId, updateId },
            cancellationToken: ct));

        var inserted = await conn.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            INSERT INTO processed_update_inbox (bot_id, update_id, status, correlation_id)
            VALUES (@botId, @updateId, 'processing', @correlationId)
            ON CONFLICT (bot_id, update_id) DO NOTHING
            RETURNING 1
            """,
            new
            {
                botId,
                updateId,
                correlationId = $"telegram-update:{botId}:{updateId}",
            },
            cancellationToken: ct));
        return inserted == 1;
    }

    private async Task MarkCompletedAsync(long updateId, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE processed_update_inbox
            SET status = 'completed', completed_at = now(), error = NULL
            WHERE bot_id = @botId AND update_id = @updateId
            """,
            new { botId, updateId },
            cancellationToken: ct));
    }

    private async Task MarkFailedAsync(long updateId, Exception ex)
    {
        try
        {
            await using var conn = await connections.OpenAsync(CancellationToken.None);
            await conn.ExecuteAsync(new CommandDefinition(
                """
                DELETE FROM processed_update_inbox
                WHERE bot_id = @botId AND update_id = @updateId AND status = 'processing'
                """,
                new { botId, updateId },
                cancellationToken: CancellationToken.None));
        }
        catch (Exception cleanupEx)
        {
            LogCleanupFailed(updateId, ex.GetType().Name, cleanupEx);
        }
    }

    [LoggerMessage(EventId = 1600, Level = LogLevel.Information, Message = "update.dedup duplicate update_id={UpdateId}")]
    partial void LogDuplicate(long updateId);

    [LoggerMessage(EventId = 1601, Level = LogLevel.Warning, Message = "update.dedup cleanup_failed update_id={UpdateId} original_error={OriginalError}")]
    partial void LogCleanupFailed(long updateId, string originalError, Exception exception);
}
