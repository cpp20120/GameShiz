namespace Games.PixelBattle.Infrastructure.Persistence;

public interface IPixelBattleStore
{
    Task<PixelBattleGrid> GetGridAsync(CancellationToken ct);
    Task<PixelBattleUpdate> UpdateTileAsync(int index, string color, long userId, CancellationToken ct);
    Task<bool> IsKnownUserAsync(long userId, CancellationToken ct);
}
