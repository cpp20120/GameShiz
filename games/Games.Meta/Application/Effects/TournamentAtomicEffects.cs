using System.Globalization;
using System.Text.Json;
using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Execution;
using Dapper;

namespace Games.Meta.Application.Effects;

public sealed record TournamentCreateAtomicEffect(
    long SeasonId,
    long ChatId,
    long CreatedBy,
    string GameKey,
    int EntryFee,
    int MaxPlayers) : IAtomicEffect;

public sealed record TournamentJoinAtomicEffect(
    long TournamentId,
    long UserId,
    long ChatId,
    string DisplayName) : IAtomicEffect;

public sealed record TournamentStartAtomicEffect(long TournamentId, long UserId) : IAtomicEffect;
public sealed record TournamentReportAtomicEffect(long MatchId, long ActorUserId, long VictorUserId) : IAtomicEffect;
public sealed record TournamentFinishAtomicEffect(long TournamentId, long ActorUserId, long VictorUserId) : IAtomicEffect;
public sealed record TournamentCancelAtomicEffect(long TournamentId, long ActorUserId) : IAtomicEffect;

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
                ct).ConfigureAwait(false);
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
            new { tournamentId }, ct).ConfigureAwait(false);
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
            }, ct).ConfigureAwait(false);

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
            ct).ConfigureAwait(false);
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
        await wallet.EnsureUserAsync(userId, chatId, displayName, ct).ConfigureAwait(false);
        var result = await wallet.ApplyBatchAsync(
            userId,
            chatId,
            [new WalletBatchEffect(WalletBatchEffectKind.Credit, amount, reason)],
            operationId,
            ct).ConfigureAwait(false);
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
            new { tournamentId, victorUserId }, ct).ConfigureAwait(false);
        await context.ExecuteAsync(
            "UPDATE meta_tournament_players SET status = 'winner' WHERE tournament_id = @tournamentId AND user_id = @victorUserId",
            new { tournamentId, victorUserId }, ct).ConfigureAwait(false);
        await context.ExecuteAsync(
            "UPDATE meta_tournaments SET status = 'finished', updated_at = now() WHERE id = @tournamentId",
            new { tournamentId }, ct).ConfigureAwait(false);
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
            new { tournamentId, nextRound, nextIndex, userId, displayName }, ct).ConfigureAwait(false);
        await context.ExecuteAsync(
            "UPDATE meta_tournament_matches SET status = 'ready', updated_at = now() WHERE tournament_id = @tournamentId AND round = @nextRound AND match_index = @nextIndex AND player1_user_id IS NOT NULL AND player2_user_id IS NOT NULL AND status = 'pending'",
            new { tournamentId, nextRound, nextIndex }, ct).ConfigureAwait(false);
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

internal sealed class TournamentCreateAtomicEffectHandler : TournamentAtomicEffectHandler<TournamentCreateAtomicEffect>
{
    protected override async Task ApplyAsync(TournamentCreateAtomicEffect effect, IAtomicEffectContext context, CancellationToken ct)
    {
        var gameKey = NormalizeGameKey(effect.GameKey);
        if (!IsSupportedGame(gameKey))
        {
            context.SetOutput("result", new TournamentCreateResult(false, "Игра для турнира пока не поддерживается. Доступно: dice, cube, darts, football, basketball, bowling."));
            return;
        }
        if (effect.EntryFee < 0 || effect.MaxPlayers is < 2 or > 64)
        {
            context.SetOutput("result", new TournamentCreateResult(false, effect.EntryFee < 0 ? "Entry fee не может быть отрицательным." : "Количество игроков должно быть от 2 до 64."));
            return;
        }

        var id = await context.QuerySingleOrDefaultAsync<long?>(
            "INSERT INTO meta_tournaments (season_id, chat_id, game_key, type, status, entry_fee, max_players, created_by) VALUES (@seasonId, @chatId, @gameKey, 'single_elimination', 'open', @entryFee, @maxPlayers, @createdBy) RETURNING id",
            new { effect.SeasonId, effect.ChatId, gameKey, effect.EntryFee, effect.MaxPlayers, effect.CreatedBy }, ct).ConfigureAwait(false);
        if (id is null) throw new InvalidOperationException("Tournament creation did not return an id.");
        var tournament = await TournamentAsync(context, id.Value, false, ct).ConfigureAwait(false);
        await AppendHistoryAsync(context, "tournament.created", effect.SeasonId, effect.ChatId, effect.CreatedBy, id.Value.ToString(CultureInfo.InvariantCulture), new { id, gameKey, effect.EntryFee, effect.MaxPlayers }, ct).ConfigureAwait(false);
        context.SetOutput("result", new TournamentCreateResult(true, "Турнир создан.", tournament));
    }
}

internal sealed class TournamentJoinAtomicEffectHandler : TournamentAtomicEffectHandler<TournamentJoinAtomicEffect>
{
    protected override async Task ApplyAsync(TournamentJoinAtomicEffect effect, IAtomicEffectContext context, CancellationToken ct)
    {
        var tournament = await TournamentAsync(context, effect.TournamentId, true, ct).ConfigureAwait(false);
        if (tournament is null)
        {
            context.SetOutput("result", new TournamentJoinResult(false, "Турнир не найден."));
            return;
        }
        if (tournament.ChatId != effect.ChatId)
        {
            context.SetOutput("result", new TournamentJoinResult(false, "Этот турнир создан в другом чате."));
            return;
        }
        if (!string.Equals(tournament.Status, "open", StringComparison.Ordinal))
        {
            context.SetOutput("result", new TournamentJoinResult(false, "Турнир уже не открыт для регистрации."));
            return;
        }
        if (tournament.PlayerCount >= tournament.MaxPlayers)
        {
            context.SetOutput("result", new TournamentJoinResult(false, "Турнир уже заполнен.", tournament));
            return;
        }
        var exists = await context.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meta_tournament_players WHERE tournament_id = @tournamentId AND user_id = @userId",
            new { effect.TournamentId, effect.UserId }, ct).ConfigureAwait(false);
        if (exists > 0)
        {
            context.SetOutput("result", new TournamentJoinResult(false, "Ты уже зарегистрирован в этом турнире.", tournament));
            return;
        }

        var wallet = context.Wallet ?? throw new InvalidOperationException("Wallet boundary is not configured.");
        await wallet.EnsureUserAsync(effect.UserId, effect.ChatId, effect.DisplayName, ct).ConfigureAwait(false);
        if (!await TryDebitAsync(context, effect.UserId, effect.ChatId, tournament.EntryFee, "tournament.entry_fee", $"tournament:entry:{tournament.Id}:{effect.ChatId}:{effect.UserId}", ct).ConfigureAwait(false))
        {
            context.SetOutput("result", new TournamentJoinResult(false, "Недостаточно монет для entry fee.", tournament));
            return;
        }

        await context.ExecuteAsync(
            "INSERT INTO meta_tournament_players (tournament_id, user_id, display_name) VALUES (@tournamentId, @userId, @displayName)",
            new { effect.TournamentId, effect.UserId, effect.DisplayName }, ct).ConfigureAwait(false);
        var updated = await TournamentAsync(context, effect.TournamentId, false, ct).ConfigureAwait(false);
        await AppendHistoryAsync(context, "tournament.joined", tournament.SeasonId, effect.ChatId, effect.UserId, tournament.Id.ToString(CultureInfo.InvariantCulture), new { effect.TournamentId, effect.DisplayName, tournament.EntryFee }, ct).ConfigureAwait(false);
        context.SetOutput("result", new TournamentJoinResult(true, "Ты зарегистрирован в турнире.", updated));
    }
}

internal sealed class TournamentStartAtomicEffectHandler : TournamentAtomicEffectHandler<TournamentStartAtomicEffect>
{
    protected override async Task ApplyAsync(TournamentStartAtomicEffect effect, IAtomicEffectContext context, CancellationToken ct)
    {
        var tournament = await TournamentAsync(context, effect.TournamentId, true, ct).ConfigureAwait(false);
        if (tournament is null || tournament.CreatedBy != effect.UserId || !string.Equals(tournament.Status, "open", StringComparison.Ordinal))
        {
            context.SetOutput("result", false);
            return;
        }
        var existing = await context.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meta_tournament_matches WHERE tournament_id = @tournamentId", new { effect.TournamentId }, ct).ConfigureAwait(false);
        if (existing > 0) { context.SetOutput("result", false); return; }
        var players = await context.QueryAsync<TournamentPlayerInfo>(
            "SELECT tournament_id AS TournamentId, user_id AS UserId, display_name AS DisplayName, status, joined_at AS JoinedAt FROM meta_tournament_players WHERE tournament_id = @tournamentId AND status = 'joined' ORDER BY joined_at ASC",
            new { effect.TournamentId }, ct).ConfigureAwait(false);
        if (players.Count < 2) { context.SetOutput("result", false); return; }

        await context.ExecuteAsync("UPDATE meta_tournaments SET status = 'started', updated_at = now() WHERE id = @tournamentId", new { effect.TournamentId }, ct).ConfigureAwait(false);
        var size = NextPowerOfTwo(players.Count);
        var rounds = (int)Math.Log2(size);
        for (var round = 1; round <= rounds; round++)
        {
            var count = size / (int)Math.Pow(2, round);
            for (var index = 1; index <= count; index++)
                await context.ExecuteAsync("INSERT INTO meta_tournament_matches (tournament_id, round, match_index, status) VALUES (@tournamentId, @round, @index, 'pending')", new { effect.TournamentId, round, index }, ct).ConfigureAwait(false);
        }
        for (var i = 0; i < size; i += 2)
        {
            var p1 = i < players.Count ? players[i] : null;
            var p2 = i + 1 < players.Count ? players[i + 1] : null;
            var index = i / 2 + 1;
            if (p1 is not null && p2 is not null)
                await context.ExecuteAsync("UPDATE meta_tournament_matches SET status = 'ready', player1_user_id = @p1id, player1_display_name = @p1name, player2_user_id = @p2id, player2_display_name = @p2name, updated_at = now() WHERE tournament_id = @tournamentId AND round = 1 AND match_index = @index", new { effect.TournamentId, index, p1id = p1.UserId, p1name = p1.DisplayName, p2id = p2.UserId, p2name = p2.DisplayName }, ct).ConfigureAwait(false);
            else if (p1 is not null)
            {
                await context.ExecuteAsync("UPDATE meta_tournament_matches SET status = 'byed', player1_user_id = @p1id, player1_display_name = @p1name, victor_user_id = @p1id, updated_at = now() WHERE tournament_id = @tournamentId AND round = 1 AND match_index = @index", new { effect.TournamentId, index, p1id = p1.UserId, p1name = p1.DisplayName }, ct).ConfigureAwait(false);
                if (rounds == 1) await CompleteTournamentAsync(context, effect.TournamentId, p1.UserId, ct).ConfigureAwait(false);
                else await AdvanceAsync(context, effect.TournamentId, 1, index, p1.UserId, p1.DisplayName, ct).ConfigureAwait(false);
            }
        }
        await AppendHistoryAsync(context, "tournament.started", tournament.SeasonId, tournament.ChatId, effect.UserId, effect.TournamentId.ToString(CultureInfo.InvariantCulture), new { effect.TournamentId, tournament.GameKey, tournament.PlayerCount, tournament.MaxPlayers }, ct).ConfigureAwait(false);
        context.SetOutput("result", true);
    }
}

internal sealed class TournamentReportAtomicEffectHandler : TournamentAtomicEffectHandler<TournamentReportAtomicEffect>
{
    protected override async Task ApplyAsync(TournamentReportAtomicEffect effect, IAtomicEffectContext context, CancellationToken ct)
    {
        var match = await MatchAsync(context, effect.MatchId, true, ct).ConfigureAwait(false);
        if (match is null) { context.SetOutput("result", new TournamentReportResult(false, false, "Матч не найден.")); return; }
        var tournament = await TournamentAsync(context, match.TournamentId, true, ct).ConfigureAwait(false);
        if (tournament is null || tournament.CreatedBy != effect.ActorUserId || !string.Equals(tournament.Status, "started", StringComparison.Ordinal))
        { context.SetOutput("result", new TournamentReportResult(false, false, "Нужен creator и started-турнир.")); return; }
        if (!string.Equals(match.Status, "ready", StringComparison.Ordinal) || match.Player1UserId is null || match.Player2UserId is null)
        { context.SetOutput("result", new TournamentReportResult(false, false, "Матч не готов к репорту.")); return; }
        if (effect.VictorUserId != match.Player1UserId && effect.VictorUserId != match.Player2UserId)
        { context.SetOutput("result", new TournamentReportResult(false, false, "Игрок не участвует в этом матче.")); return; }
        var victorName = effect.VictorUserId == match.Player1UserId ? match.Player1DisplayName! : match.Player2DisplayName!;
        await context.ExecuteAsync("UPDATE meta_tournament_matches SET status = 'finished', victor_user_id = @victorUserId, updated_at = now() WHERE id = @matchId", new { effect.MatchId, effect.VictorUserId }, ct).ConfigureAwait(false);
        var maxRound = await context.QuerySingleOrDefaultAsync<int>("SELECT max(round)::int FROM meta_tournament_matches WHERE tournament_id = @tournamentId", new { match.TournamentId }, ct).ConfigureAwait(false);
        var finished = match.Round >= maxRound;
        if (finished) await CompleteTournamentAsync(context, match.TournamentId, effect.VictorUserId, ct).ConfigureAwait(false);
        else await AdvanceAsync(context, match.TournamentId, match.Round, match.MatchIndex, effect.VictorUserId, victorName, ct).ConfigureAwait(false);
        if (finished && tournament.PrizePool > 0)
            await CreditAsync(context, effect.VictorUserId, tournament.ChatId, victorName, checked((int)Math.Min(int.MaxValue, tournament.PrizePool)), "tournament.prize", $"tournament:prize:{tournament.Id}:{effect.VictorUserId}", ct).ConfigureAwait(false);
        var updatedMatch = await MatchAsync(context, effect.MatchId, false, ct).ConfigureAwait(false);
        var victor = await PlayerAsync(context, match.TournamentId, effect.VictorUserId, false, ct).ConfigureAwait(false);
        await AppendHistoryAsync(context, finished ? "tournament.finished" : "tournament.match_reported", tournament.SeasonId, tournament.ChatId, effect.VictorUserId, tournament.Id.ToString(CultureInfo.InvariantCulture), new { effect.MatchId, effect.VictorUserId, finished }, ct).ConfigureAwait(false);
        context.SetOutput("result", new TournamentReportResult(true, finished, finished ? "Турнир завершён." : "Матч засчитан, игрок продвинут дальше.", updatedMatch, victor));
    }
}

internal sealed class TournamentFinishAtomicEffectHandler : TournamentAtomicEffectHandler<TournamentFinishAtomicEffect>
{
    protected override async Task ApplyAsync(TournamentFinishAtomicEffect effect, IAtomicEffectContext context, CancellationToken ct)
    {
        var tournament = await TournamentAsync(context, effect.TournamentId, true, ct).ConfigureAwait(false);
        var player = await PlayerAsync(context, effect.TournamentId, effect.VictorUserId, true, ct).ConfigureAwait(false);
        if (tournament is null || player is null || tournament.CreatedBy != effect.ActorUserId || !string.Equals(tournament.Status, "started", StringComparison.Ordinal) || !string.Equals(player.Status, "joined", StringComparison.Ordinal))
        { context.SetOutput("result", null); return; }
        await CompleteTournamentAsync(context, effect.TournamentId, effect.VictorUserId, ct).ConfigureAwait(false);
        if (tournament.PrizePool > 0)
            await CreditAsync(context, effect.VictorUserId, tournament.ChatId, player.DisplayName, checked((int)Math.Min(int.MaxValue, tournament.PrizePool)), "tournament.prize", $"tournament:prize:{tournament.Id}:{effect.VictorUserId}", ct).ConfigureAwait(false);
        await AppendHistoryAsync(context, "tournament.finished", tournament.SeasonId, tournament.ChatId, effect.VictorUserId, tournament.Id.ToString(CultureInfo.InvariantCulture), new { effect.TournamentId, effect.VictorUserId, tournament.PrizePool, via = "manual" }, ct).ConfigureAwait(false);
        context.SetOutput("result", player with { Status = "winner" });
    }
}

internal sealed class TournamentCancelAtomicEffectHandler : TournamentAtomicEffectHandler<TournamentCancelAtomicEffect>
{
    protected override async Task ApplyAsync(TournamentCancelAtomicEffect effect, IAtomicEffectContext context, CancellationToken ct)
    {
        var tournament = await TournamentAsync(context, effect.TournamentId, true, ct).ConfigureAwait(false);
        if (tournament is null || tournament.CreatedBy != effect.ActorUserId || tournament.Status is not ("open" or "started"))
        { context.SetOutput("result", null); return; }
        var players = await context.QueryAsync<TournamentPlayerInfo>(
            "SELECT tournament_id AS TournamentId, user_id AS UserId, display_name AS DisplayName, status, joined_at AS JoinedAt FROM meta_tournament_players WHERE tournament_id = @tournamentId AND status = 'joined' ORDER BY joined_at ASC",
            new { effect.TournamentId }, ct).ConfigureAwait(false);
        await context.ExecuteAsync("UPDATE meta_tournaments SET status = 'cancelled', updated_at = now() WHERE id = @tournamentId", new { effect.TournamentId }, ct).ConfigureAwait(false);
        if (tournament.EntryFee > 0)
            foreach (var player in players)
                await CreditAsync(context, player.UserId, tournament.ChatId, player.DisplayName, tournament.EntryFee, "tournament.cancel.refund", $"tournament:cancel-refund:{tournament.Id}:{player.UserId}", ct).ConfigureAwait(false);
        await AppendHistoryAsync(context, "tournament.cancelled", tournament.SeasonId, tournament.ChatId, effect.ActorUserId, tournament.Id.ToString(CultureInfo.InvariantCulture), new { effect.TournamentId, tournament.EntryFee, refundedPlayers = players.Select(x => x.UserId).ToArray() }, ct).ConfigureAwait(false);
        context.SetOutput("result", players);
    }
}
