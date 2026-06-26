namespace BotFramework.Sdk.MiniGames;

public interface IMiniGameSessionGhostHeal
{
    /// <summary>
    /// Clears <see cref="BotMiniGameSession"/> when it still points at <paramref name="blockingGameId"/>
    /// but persistence has no pending work (restart / missed cleanup drift).
    /// </summary>
    Task<bool> TryClearGhostIfDbEmptyAsync(long userId, long chatId, string blockingGameId, CancellationToken ct);
}
