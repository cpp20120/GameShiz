using System.Text.Json;
using Dapper;
using Npgsql;

namespace Games.Pick.Infrastructure.Persistence;

public sealed class PickChainStore(INpgsqlConnectionFactory connections)
{
    public async Task<PickChainState?> ClaimAsync(Guid id, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            """
            WITH claimed AS (
                DELETE FROM pick_chains
                WHERE id = @id
                RETURNING id, user_id, chat_id, display_name, stake_for_next, depth,
                          variants_json, backed_indices_json, expires_at
            )
            SELECT id AS Id, user_id AS UserId, chat_id AS ChatId, display_name AS DisplayName,
                   stake_for_next AS StakeForNext, depth AS Depth,
                   variants_json::text AS VariantsJson, backed_indices_json::text AS BackedJson,
                   expires_at AS ExpiresAt
            FROM claimed
            WHERE expires_at >= now()
            """,
            new { id }, cancellationToken: ct)).ConfigureAwait(false);
        return row?.ToState();
    }

    public async Task RestoreAsync(PickChainState state, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO pick_chains
                (id, user_id, chat_id, display_name, stake_for_next, depth, variants_json, backed_indices_json, expires_at)
            VALUES
                (@Id, @UserId, @ChatId, @DisplayName, @StakeForNext, @Depth,
                 CAST(@variantsJson AS jsonb), CAST(@backedJson AS jsonb), @ExpiresAt)
            ON CONFLICT (id) DO UPDATE SET expires_at = EXCLUDED.expires_at
            """,
            new
            {
                state.Id,
                state.UserId,
                state.ChatId,
                state.DisplayName,
                state.StakeForNext,
                state.Depth,
                variantsJson = JsonSerializer.Serialize(state.Variants),
                backedJson = JsonSerializer.Serialize(state.BackedIndices),
                state.ExpiresAt,
            }, cancellationToken: ct)).ConfigureAwait(false);
    }

    private sealed record Row(
        Guid Id,
        long UserId,
        long ChatId,
        string DisplayName,
        int StakeForNext,
        int Depth,
        string VariantsJson,
        string BackedJson,
        DateTime ExpiresAt)
    {
        public PickChainState ToState() => new(
            Id, UserId, ChatId, DisplayName, StakeForNext, Depth,
            JsonSerializer.Deserialize<string[]>(VariantsJson) ?? [],
            JsonSerializer.Deserialize<int[]>(BackedJson) ?? [],
            new DateTimeOffset(DateTime.SpecifyKind(ExpiresAt, DateTimeKind.Utc)));
    }
}
