using System.Globalization;
using System.Text.Json;
using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Execution;
using Dapper;

namespace Games.Meta.Application.Effects;

internal sealed class TournamentStartAtomicEffectHandler : TournamentAtomicEffectHandler<TournamentStartAtomicEffect>
{
    protected override async Task ApplyAsync(TournamentStartAtomicEffect effect, IAtomicEffectContext context, CancellationToken ct)
    {
        var tournament = await TournamentAsync(context, effect.TournamentId, true, ct);
        if (tournament is null || tournament.CreatedBy != effect.UserId || !string.Equals(tournament.Status, "open", StringComparison.Ordinal))
        {
            context.SetOutput("result", false);
            return;
        }
        var existing = await context.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meta_tournament_matches WHERE tournament_id = @tournamentId", new { effect.TournamentId }, ct);
        if (existing > 0) { context.SetOutput("result", false); return; }
        var players = await context.QueryAsync<TournamentPlayerInfo>(
            "SELECT tournament_id AS TournamentId, user_id AS UserId, display_name AS DisplayName, status, joined_at AS JoinedAt FROM meta_tournament_players WHERE tournament_id = @tournamentId AND status = 'joined' ORDER BY joined_at ASC",
            new { effect.TournamentId }, ct);
        if (players.Count < 2) { context.SetOutput("result", false); return; }

        await context.ExecuteAsync("UPDATE meta_tournaments SET status = 'started', updated_at = now() WHERE id = @tournamentId", new { effect.TournamentId }, ct);
        var size = NextPowerOfTwo(players.Count);
        var rounds = (int)Math.Log2(size);
        for (var round = 1; round <= rounds; round++)
        {
            var count = size / (int)Math.Pow(2, round);
            for (var index = 1; index <= count; index++)
                await context.ExecuteAsync("INSERT INTO meta_tournament_matches (tournament_id, round, match_index, status) VALUES (@tournamentId, @round, @index, 'pending')", new { effect.TournamentId, round, index }, ct);
        }
        for (var i = 0; i < size; i += 2)
        {
            var p1 = i < players.Count ? players[i] : null;
            var p2 = i + 1 < players.Count ? players[i + 1] : null;
            var index = i / 2 + 1;
            if (p1 is not null && p2 is not null)
                await context.ExecuteAsync("UPDATE meta_tournament_matches SET status = 'ready', player1_user_id = @p1id, player1_display_name = @p1name, player2_user_id = @p2id, player2_display_name = @p2name, updated_at = now() WHERE tournament_id = @tournamentId AND round = 1 AND match_index = @index", new { effect.TournamentId, index, p1id = p1.UserId, p1name = p1.DisplayName, p2id = p2.UserId, p2name = p2.DisplayName }, ct);
            else if (p1 is not null)
            {
                await context.ExecuteAsync("UPDATE meta_tournament_matches SET status = 'byed', player1_user_id = @p1id, player1_display_name = @p1name, victor_user_id = @p1id, updated_at = now() WHERE tournament_id = @tournamentId AND round = 1 AND match_index = @index", new { effect.TournamentId, index, p1id = p1.UserId, p1name = p1.DisplayName }, ct);
                if (rounds == 1) await CompleteTournamentAsync(context, effect.TournamentId, p1.UserId, ct);
                else await AdvanceAsync(context, effect.TournamentId, 1, index, p1.UserId, p1.DisplayName, ct);
            }
        }
        await AppendHistoryAsync(context, "tournament.started", tournament.SeasonId, tournament.ChatId, effect.UserId, effect.TournamentId.ToString(CultureInfo.InvariantCulture), new { effect.TournamentId, tournament.GameKey, tournament.PlayerCount, tournament.MaxPlayers }, ct);
        context.SetOutput("result", true);
    }
}
