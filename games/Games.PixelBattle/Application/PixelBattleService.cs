using Games.PixelBattle.Contracts;

namespace Games.PixelBattle.Application;

public sealed class PixelBattleService(IPixelBattleStore store) : IPixelBattleService
{
    public Task<PixelBattleGrid> GetGridAsync(CancellationToken ct) => store.GetGridAsync(ct);

    public async Task<PixelUpdateResult> UpdateAsync(
        long userId, int index, string color, CancellationToken ct)
    {
        if (!PixelBattleConstants.IsValidIndex(index))
            return new PixelUpdateResult(PixelUpdateStatus.InvalidIndex);
        if (!PixelBattleConstants.IsValidColor(color))
            return new PixelUpdateResult(PixelUpdateStatus.InvalidColor);
        if (!await store.IsKnownUserAsync(userId, ct))
            return new PixelUpdateResult(PixelUpdateStatus.UnknownUser);

        return new PixelUpdateResult(
            PixelUpdateStatus.Updated,
            await store.UpdateTileAsync(index, color, userId, ct));
    }
}
