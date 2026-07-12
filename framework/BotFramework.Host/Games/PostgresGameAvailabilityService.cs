using BotFramework.Contracts.Games;
using BotFramework.Host.Admin.Audit;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BotFramework.Host.Games;

public sealed partial class PostgresGameAvailabilityService(
    INpgsqlConnectionFactory connections,
    IConfiguration configuration,
    IAdminAuditLog audit,
    TimeProvider timeProvider,
    ILogger<PostgresGameAvailabilityService> logger) : IGameAvailabilityService, IGameAvailabilityClient
{
    public async Task<GameAvailability> GetAsync(long chatId, string gameId, CancellationToken ct = default)
    {
        ValidateGameId(gameId);
        try
        {
            const string sql = """
                SELECT enabled AS "Enabled", reason AS "Reason", changed_by AS "ChangedBy",
                       changed_at AS "ChangedAt"
                FROM game_availability_overrides
                WHERE chat_id = @chatId AND game_id = @gameId
                """;
            await using var connection = await connections.OpenAsync(ct);
            var row = await connection.QuerySingleOrDefaultAsync<OverrideRow>(
                new CommandDefinition(sql, new { chatId, gameId }, cancellationToken: ct));
            return row is null
                ? ConfigDefault(chatId, gameId, GameAvailabilitySource.Configuration)
                : new(chatId, gameId, row.Enabled, GameAvailabilitySource.ChatOverride,
                    row.Reason, row.ChangedBy, row.ChangedAt);
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
        {
            LogFallback(logger, chatId, gameId, exception.Message);
            return ConfigDefault(chatId, gameId, GameAvailabilitySource.ConfigurationFallback);
        }
    }

    public async Task<IReadOnlyList<GameAvailability>> ListOverridesAsync(long? chatId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT chat_id AS "ChatId", game_id AS "GameId", enabled AS "Enabled",
                   reason AS "Reason", changed_by AS "ChangedBy", changed_at AS "ChangedAt"
            FROM game_availability_overrides
            WHERE @chatId IS NULL OR chat_id = @chatId
            ORDER BY chat_id, game_id
            """;
        await using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<OverrideListRow>(
            new CommandDefinition(sql, new { chatId }, cancellationToken: ct));
        return rows.Select(row => new GameAvailability(row.ChatId, row.GameId, row.Enabled,
            GameAvailabilitySource.ChatOverride, row.Reason, row.ChangedBy, row.ChangedAt)).ToList();
    }

    public async Task<GameAvailability> SetOverrideAsync(SetGameAvailability command, CancellationToken ct = default)
    {
        ValidateGameId(command.GameId);
        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new ArgumentException("An availability override requires a reason.", nameof(command));

        const string sql = """
            INSERT INTO game_availability_overrides
                (chat_id, game_id, enabled, reason, changed_by, changed_at)
            VALUES (@ChatId, @GameId, @Enabled, @Reason, @ActorId, @changedAt)
            ON CONFLICT (chat_id, game_id) DO UPDATE SET
                enabled = EXCLUDED.enabled, reason = EXCLUDED.reason,
                changed_by = EXCLUDED.changed_by, changed_at = EXCLUDED.changed_at
            """;
        var changedAt = timeProvider.GetUtcNow();
        await using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            command.ChatId, command.GameId, command.Enabled, command.Reason, command.ActorId, changedAt,
        }, cancellationToken: ct));
        await audit.LogAsync(command.ActorId, command.ActorName, "game.availability.set", new
        {
            command.ChatId, command.GameId, command.Enabled, command.Reason,
        }, ct);
        return new(command.ChatId, command.GameId, command.Enabled, GameAvailabilitySource.ChatOverride,
            command.Reason, command.ActorId, changedAt);
    }

    public async Task RemoveOverrideAsync(long chatId, string gameId, long actorId, string actorName,
        CancellationToken ct = default)
    {
        ValidateGameId(gameId);
        const string sql = "DELETE FROM game_availability_overrides WHERE chat_id=@chatId AND game_id=@gameId";
        await using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { chatId, gameId }, cancellationToken: ct));
        await audit.LogAsync(actorId, actorName, "game.availability.remove", new { chatId, gameId }, ct);
    }

    private GameAvailability ConfigDefault(long chatId, string gameId, GameAvailabilitySource source) =>
        new(chatId, gameId, configuration.GetValue($"Games:{gameId}:Enabled", true), source);

    private static void ValidateGameId(string gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId) || gameId.Length > 100)
            throw new ArgumentException("A game ID of at most 100 characters is required.", nameof(gameId));
    }

    [LoggerMessage(EventId = 1800, Level = LogLevel.Warning,
        Message = "Game availability database unavailable; using config default. chat={ChatId} game={GameId} error={Error}")]
    private static partial void LogFallback(ILogger logger, long chatId, string gameId, string error);

    private sealed record OverrideRow(bool Enabled, string Reason, long ChangedBy, DateTimeOffset ChangedAt);
    private sealed record OverrideListRow(long ChatId, string GameId, bool Enabled, string Reason,
        long ChangedBy, DateTimeOffset ChangedAt);
}
