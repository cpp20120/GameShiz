using System.Collections.Concurrent;

namespace BotFramework.Sdk.MiniGames;

/// <summary>
/// One pending dice mini-game per (user, chat, game): prevents duplicate rounds of the same game
/// and stale "already pending" confusion. Different games in the same chat are independent.
/// </summary>
public static class BotMiniGameSession
{
    private sealed record Row(long ExpiresTicks);

    private static readonly ConcurrentDictionary<(long UserId, long ChatId, string GameId), Row> Map = new();

    /// <summary>Test isolation — xUnit constructs a new class instance per test; call from test ctor if tests hit <see cref="TryBeginPlaceBet"/>.</summary>
    public static void DangerousResetAllForTests() => Map.Clear();

    public const int TtlMs = 180_000;

    /// <summary>
    /// Call before debiting for a new bet. The key is scoped to the game, so another game
    /// can start in the same chat while this one is waiting for its bot roll.
    /// </summary>
    public static bool TryBeginPlaceBet(long userId, long chatId, string gameId, out string? blockingGameId)
    {
        blockingGameId = null;
        var key = (userId, chatId, gameId);
        PruneKey(key);
        return true;
    }

    public static void RegisterPlacedBet(long userId, long chatId, string gameId) =>
        Map[(userId, chatId, gameId)] = new Row(Environment.TickCount64 + TtlMs);

    public static void ClearCompletedRound(long userId, long chatId, string gameId)
    {
        Map.TryRemove((userId, chatId, gameId), out _);
    }

    private static void PruneKey((long UserId, long ChatId, string GameId) key)
    {
        if (Map.TryGetValue(key, out var row) && Environment.TickCount64 > row.ExpiresTicks)
            Map.TryRemove(key, out _);
    }
}
