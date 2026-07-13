namespace Games.PixelBattle.Contracts;

public enum PixelUpdateStatus
{
    Updated,
    InvalidIndex,
    InvalidColor,
    UnknownUser,
}

public sealed record PixelUpdateResult(PixelUpdateStatus Status, PixelBattleUpdate? Update = null);

public interface IPixelBattleService
{
    Task<PixelBattleGrid> GetGridAsync(CancellationToken ct);
    Task<PixelUpdateResult> UpdateAsync(long userId, int index, string color, CancellationToken ct);
}
