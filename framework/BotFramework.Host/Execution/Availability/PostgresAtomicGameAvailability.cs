using Dapper;
using BotFramework.Contracts.Games;
using Microsoft.Extensions.Configuration;

namespace BotFramework.Host.Execution;

internal sealed class PostgresAtomicGameAvailability(IConfiguration configuration) : IAtomicGameAvailability
{
    public async Task<GameAvailability> GetAsync(
        long chatId,
        string gameId,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        const string sql = """
            SELECT enabled AS Enabled,
                   reason AS Reason,
                   changed_by AS ChangedBy,
                   changed_at AS ChangedAt
            FROM game_availability_overrides
            WHERE chat_id = @chatId
              AND game_id = @gameId
            """;
        var row = await session.Connection.QuerySingleOrDefaultAsync<AvailabilityRow>(new CommandDefinition(
            sql,
            new { chatId, gameId },
            session.Transaction,
            cancellationToken: ct)).ConfigureAwait(false);

        return row is null
            ? new GameAvailability(
                chatId,
                gameId,
                configuration.GetValue($"Games:{gameId}:Enabled", true),
                GameAvailabilitySource.Configuration)
            : new GameAvailability(
                chatId,
                gameId,
                row.Enabled,
                GameAvailabilitySource.ChatOverride,
                row.Reason,
                row.ChangedBy,
                row.ChangedAt);
    }

    private sealed record AvailabilityRow(
        bool Enabled,
        string Reason,
        long ChangedBy,
        DateTimeOffset ChangedAt);
}
