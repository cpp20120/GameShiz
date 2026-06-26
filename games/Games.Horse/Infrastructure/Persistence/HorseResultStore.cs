using Dapper;

namespace Games.Horse.Infrastructure.Persistence;

public sealed class HorseResultStore(INpgsqlConnectionFactory connections) : IHorseResultStore
{
    public async Task<HorseResultRow?> FindAsync(string raceDate, long balanceScopeId, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<HorseResultRow>(new CommandDefinition(
            """
            SELECT race_date AS RaceDate, balance_scope_id AS BalanceScopeId,
                   winner AS Winner, file_id AS FileId
            FROM horse_results
            WHERE race_date = @raceDate AND balance_scope_id = @balanceScopeId
            """,
            new { raceDate, balanceScopeId },
            cancellationToken: ct));
    }

    public async Task UpsertAsync(HorseResultRow result, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO horse_results (race_date, balance_scope_id, winner)
            VALUES (@RaceDate, @BalanceScopeId, @Winner)
            ON CONFLICT (race_date, balance_scope_id) DO UPDATE SET
                winner = EXCLUDED.winner
            """,
            result,
            cancellationToken: ct));
    }

    public async Task SaveFileIdAsync(string raceDate, long balanceScopeId, string fileId, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE horse_results SET file_id = @fileId
            WHERE race_date = @raceDate AND balance_scope_id = @balanceScopeId
            """,
            new { raceDate, balanceScopeId, fileId },
            cancellationToken: ct));
    }
}
