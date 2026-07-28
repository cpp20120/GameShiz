using System.Globalization;
using System.Text.Json;
using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Execution;
using Dapper;

namespace Games.Meta.Application.Effects;

internal sealed class TournamentCancelAtomicEffectHandler : TournamentAtomicEffectHandler<TournamentCancelAtomicEffect>
{
    protected override async Task ApplyAsync(TournamentCancelAtomicEffect effect, IAtomicEffectContext context, CancellationToken ct)
    {
        var tournament = await TournamentAsync(context, effect.TournamentId, true, ct);
        if (tournament is null || tournament.CreatedBy != effect.ActorUserId || tournament.Status is not ("open" or "started"))
        { context.SetOutput("result", null); return; }
        var players = await context.QueryAsync<TournamentPlayerInfo>(
            "SELECT tournament_id AS TournamentId, user_id AS UserId, display_name AS DisplayName, status, joined_at AS JoinedAt FROM meta_tournament_players WHERE tournament_id = @tournamentId AND status = 'joined' ORDER BY joined_at ASC",
            new { effect.TournamentId }, ct);
        await context.ExecuteAsync("UPDATE meta_tournaments SET status = 'cancelled', updated_at = now() WHERE id = @tournamentId", new { effect.TournamentId }, ct);
        if (tournament.EntryFee > 0 && !effect.RefundsAlreadyPaid)
            foreach (var player in players)
                await CreditAsync(context, player.UserId, tournament.ChatId, player.DisplayName, tournament.EntryFee, "tournament.cancel.refund", $"tournament:cancel-refund:{tournament.Id}:{player.UserId}", ct);
        await AppendHistoryAsync(context, "tournament.cancelled", tournament.SeasonId, tournament.ChatId, effect.ActorUserId, tournament.Id.ToString(CultureInfo.InvariantCulture), new { effect.TournamentId, tournament.EntryFee, refundedPlayers = players.Select(x => x.UserId).ToArray() }, ct);
        context.SetOutput("result", players);
    }
}
