namespace Games.PixelBattle.Contracts;

public interface IPixelBattleService
{
    Task<PixelBattleGrid> GetGridAsync(CancellationToken ct);
    Task<PixelUpdateResult> UpdateAsync(long userId, int index, string color, CancellationToken ct);
}
