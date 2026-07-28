using System.Globalization;
using System.Text.Json;
using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Execution;
using Dapper;

namespace Games.Meta.Application.Effects;

internal abstract class TournamentAtomicEffectHandler<TEffect> : AtomicEffectHandler<TEffect>
    where TEffect : class, IAtomicEffect
{
    protected static async Task<TournamentInfo?> TournamentAsync(
        IAtomicEffectContext context,
        long tournamentId,
        bool forUpdate,
        CancellationToken ct)
    {
        if (forUpdate)
            await context.ExecuteAsync(
                "SELECT 1 FROM meta_tournaments WHERE id = @tournamentId FOR UPDATE",
                new { tournamentId },
                ct);
        return await context.QuerySingleOrDefaultAsync<TournamentInfo>(
            $$"""
            SELECT t.id, t.season_id AS SeasonId, t.chat_id AS ChatId, t.game_key AS GameKey,
                   t.type, t.status, t.entry_fee AS EntryFee, t.max_players AS MaxPlayers,
                   t.created_by AS CreatedBy, t.created_at AS CreatedAt,
                   COUNT(p.user_id)::int AS PlayerCount,
                   (COUNT(p.user_id) * t.entry_fee)::bigint AS PrizePool
            FROM meta_tournaments t
            LEFT JOIN meta_tournament_players p
                ON p.tournament_id = t.id AND p.status IN ('joined', 'winner', 'eliminated')
            WHERE t.id = @tournamentId
            GROUP BY t.id, t.season_id, t.chat_id, t.game_key, t.type, t.status,
                     t.entry_fee, t.max_players, t.created_by, t.created_at
            """,
            new { tournamentId }, ct);
    }

    protected static Task<TournamentPlayerInfo?> PlayerAsync(
        IAtomicEffectContext context,
        long tournamentId,
        long userId,
        bool forUpdate,
        CancellationToken ct)
    {
        var lockClause = forUpdate ? "FOR UPDATE" : string.Empty;
        return context.QuerySingleOrDefaultAsync<TournamentPlayerInfo>(
            $$"""
            SELECT tournament_id AS TournamentId, user_id AS UserId, display_name AS DisplayName,
                   status, joined_at AS JoinedAt
            FROM meta_tournament_players
            WHERE tournament_id = @tournamentId AND user_id = @userId
            LIMIT 1
            {{lockClause}}
            """,
            new { tournamentId, userId }, ct);
    }

    protected static Task<TournamentMatchInfo?> MatchAsync(
        IAtomicEffectContext context,
        long matchId,
        bool forUpdate,
        CancellationToken ct)
    {
        var lockClause = forUpdate ? "FOR UPDATE" : string.Empty;
        return context.QuerySingleOrDefaultAsync<TournamentMatchInfo>(
            $$"""
            SELECT id AS Id, tournament_id AS TournamentId, round AS Round, match_index AS MatchIndex,
                   status, player1_user_id AS Player1UserId, player1_display_name AS Player1DisplayName,
                   player2_user_id AS Player2UserId, player2_display_name AS Player2DisplayName,
                   victor_user_id AS VictorUserId, created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM meta_tournament_matches
            WHERE id = @matchId
            {{lockClause}}
            """,
            new { matchId }, ct);
    }

    protected static async Task AppendHistoryAsync(
        IAtomicEffectContext context,
        string eventType,
        long seasonId,
        long chatId,
        long userId,
        string aggregateId,
        object payload,
        CancellationToken ct) =>
        await context.ExecuteAsync(
            """
            INSERT INTO meta_event_log
                (event_type, aggregate_type, aggregate_id, season_id, chat_id, user_id, payload)
            VALUES (@eventType, 'tournament', @aggregateId, @seasonId, @chatId, @userId, CAST(@payload AS jsonb))
            """,
            new
            {
                eventType,
                aggregateId,
                seasonId,
                chatId,
                userId,
                payload = JsonSerializer.Serialize(payload),
            }, ct);

    protected static async Task<bool> TryDebitAsync(
        IAtomicEffectContext context,
        long userId,
        long chatId,
        int amount,
        string reason,
        string operationId,
        CancellationToken ct)
    {
        if (amount <= 0) return true;
        var wallet = context.Wallet ?? throw new InvalidOperationException("Wallet boundary is not configured.");
        var result = await wallet.ApplyBatchAsync(
            userId,
            chatId,
            [new WalletBatchEffect(WalletBatchEffectKind.Debit, amount, reason)],
            operationId,
            ct);
        return result.Applied && !result.Rejected;
    }

    protected static async Task CreditAsync(
        IAtomicEffectContext context,
        long userId,
        long chatId,
        string displayName,
        int amount,
        string reason,
        string operationId,
        CancellationToken ct)
    {
        if (amount <= 0) return;
        var wallet = context.Wallet ?? throw new InvalidOperationException("Wallet boundary is not configured.");
        await wallet.EnsureUserAsync(userId, chatId, displayName, ct);
        var result = await wallet.ApplyBatchAsync(
            userId,
            chatId,
            [new WalletBatchEffect(WalletBatchEffectKind.Credit, amount, reason)],
            operationId,
            ct);
        if (!result.Applied)
            throw new InvalidOperationException("Tournament wallet rejected a credit.");
    }

    protected static async Task CompleteTournamentAsync(
        IAtomicEffectContext context,
        long tournamentId,
        long victorUserId,
        CancellationToken ct)
    {
        await context.ExecuteAsync(
            "UPDATE meta_tournament_players SET status = 'eliminated' WHERE tournament_id = @tournamentId AND status = 'joined' AND user_id <> @victorUserId",
            new { tournamentId, victorUserId }, ct);
        await context.ExecuteAsync(
            "UPDATE meta_tournament_players SET status = 'winner' WHERE tournament_id = @tournamentId AND user_id = @victorUserId",
            new { tournamentId, victorUserId }, ct);
        await context.ExecuteAsync(
            "UPDATE meta_tournaments SET status = 'finished', updated_at = now() WHERE id = @tournamentId",
            new { tournamentId }, ct);
    }

    protected static async Task AdvanceAsync(
        IAtomicEffectContext context,
        long tournamentId,
        int round,
        int matchIndex,
        long userId,
        string displayName,
        CancellationToken ct)
    {
        var nextRound = round + 1;
        var nextIndex = (matchIndex + 1) / 2;
        var slot = matchIndex % 2 == 1
            ? "player1_user_id = @userId, player1_display_name = @displayName"
            : "player2_user_id = @userId, player2_display_name = @displayName";
        await context.ExecuteAsync(
            $"UPDATE meta_tournament_matches SET {slot}, updated_at = now() WHERE tournament_id = @tournamentId AND round = @nextRound AND match_index = @nextIndex",
            new { tournamentId, nextRound, nextIndex, userId, displayName }, ct);
        await context.ExecuteAsync(
            "UPDATE meta_tournament_matches SET status = 'ready', updated_at = now() WHERE tournament_id = @tournamentId AND round = @nextRound AND match_index = @nextIndex AND player1_user_id IS NOT NULL AND player2_user_id IS NOT NULL AND status = 'pending'",
            new { tournamentId, nextRound, nextIndex }, ct);
    }

    protected static int NextPowerOfTwo(int value)
    {
        var result = 1;
        while (result < value) result <<= 1;
        return result;
    }

    protected static string NormalizeGameKey(string gameKey) =>
        gameKey.Trim().TrimStart('/').ToLowerInvariant() switch { "cube" => "dicecube", var x => x };

    protected static bool IsSupportedGame(string gameKey) =>
        gameKey is "dice" or "dicecube" or "darts" or "football" or "basketball" or "bowling";

}
