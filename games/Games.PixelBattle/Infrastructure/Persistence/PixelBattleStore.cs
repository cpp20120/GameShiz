using System.Globalization;
using Dapper;

namespace Games.PixelBattle.Infrastructure.Persistence;

public sealed class PixelBattleStore(INpgsqlConnectionFactory connections) : IPixelBattleStore
{
    public async Task<PixelBattleGrid> GetGridAsync(CancellationToken ct)
    {
        var tiles = Enumerable.Repeat(PixelBattleConstants.DefaultColor, PixelBattleConstants.Width * PixelBattleConstants.Height)
            .ToArray();
        var versionstamps = Enumerable.Repeat("", PixelBattleConstants.Width * PixelBattleConstants.Height).ToArray();

        await using var conn = await connections.OpenAsync(ct);
        var rows = await conn.QueryAsync<TileRow>(new CommandDefinition("""
            SELECT index, color, version
            FROM pixelbattle_tiles
            WHERE index >= 0 AND index < @maxIndex
            """, new { maxIndex = PixelBattleConstants.Width * PixelBattleConstants.Height }, cancellationToken: ct));

        foreach (var row in rows)
        {
            tiles[row.Index] = row.Color;
            versionstamps[row.Index] = FormatVersion(row.Version);
        }

        return new PixelBattleGrid(tiles, versionstamps);
    }

    private static string FormatVersion(long version) => version.ToString("D20", CultureInfo.InvariantCulture);

    private sealed record TileRow(int Index, string Color, long Version);
}
