namespace Games.PixelBattle.Infrastructure.Persistence;

public interface IPixelBattleStore
{
    Task<PixelBattleGrid> GetGridAsync(CancellationToken ct);
}
