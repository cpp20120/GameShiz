using System.Globalization;
using System.Text.Json;
using Dapper;
using Games.Blackjack.Application.Execution;

namespace Games.Blackjack.Infrastructure.Persistence;

public sealed class BlackjackStateReader(INpgsqlConnectionFactory connections) : IBlackjackStateReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BlackjackGameState?> LoadAsync(long userId, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct).ConfigureAwait(false);
        var json = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            """
            SELECT state::text
            FROM game_aggregate_states
            WHERE game_id = 'blackjack' AND aggregate_id = @aggregateId
            """,
            new { aggregateId = userId.ToString(CultureInfo.InvariantCulture) },
            cancellationToken: ct)).ConfigureAwait(false);
        return json is null
            ? null
            : JsonSerializer.Deserialize<BlackjackGameState>(json, JsonOptions)
                ?? throw new InvalidOperationException("Stored blackjack state is null.");
    }
}
