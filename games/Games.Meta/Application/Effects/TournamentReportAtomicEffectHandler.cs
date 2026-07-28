using System.Globalization;
using System.Text.Json;
using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Execution;
using Dapper;

namespace Games.Meta.Application.Effects;

internal sealed class TournamentReportAtomicEffectHandler : TournamentAtomicEffectHandler<TournamentReportAtomicEffect>
{
    protected override async Task ApplyAsync(TournamentReportAtomicEffect effect, IAtomicEffectContext context, CancellationToken ct)
    {
        var match = await MatchAsync(context, effect.MatchId, true, ct);
        if (match is null) { context.SetOutput("result", new TournamentReportResult(false, false, "Матч не найден.")); return; }
        var tournament = await TournamentAsync(context, match.TournamentId, true, ct);
        if (tournament is null || tournament.CreatedBy != effect.ActorUserId || !string.Equals(tournament.Status, "started", StringComparison.Ordinal))
        { context.SetOutput("result", new TournamentReportResult(false, false, "Нужен creator и started-турнир.")); return; }
        if (!string.Equals(match.Status, "ready", StringComparison.Ordinal) || match.Player1UserId is null || match.Player2UserId is null)
        { context.SetOutput("result", new TournamentReportResult(false, false, "Матч не готов к репорту.")); return; }
        if (effect.VictorUserId != match.Player1UserId && effect.VictorUserId != match.Player2UserId)
        { context.SetOutput("result", new TournamentReportResult(false, false, "Игрок не участвует в этом матче.")); return; }
        var victorName = effect.VictorUserId == match.Player1UserId ? match.Player1DisplayName! : match.Player2DisplayName!;
        await context.ExecuteAsync("UPDATE meta_tournament_matches SET status = 'finished', victor_user_id = @victorUserId, updated_at = now() WHERE id = @matchId", new { effect.MatchId, effect.VictorUserId }, ct);
        var maxRound = await context.QuerySingleOrDefaultAsync<int>("SELECT max(round)::int FROM meta_tournament_matches WHERE tournament_id = @tournamentId", new { match.TournamentId }, ct);
        var finished = match.Round >= maxRound;
        if (finished) await CompleteTournamentAsync(context, match.TournamentId, effect.VictorUserId, ct);
        else await AdvanceAsync(context, match.TournamentId, match.Round, match.MatchIndex, effect.VictorUserId, victorName, ct);
        if (finished && tournament.PrizePool > 0 && !effect.PrizeAlreadyPaid)
            await CreditAsync(context, effect.VictorUserId, tournament.ChatId, victorName, checked((int)Math.Min(int.MaxValue, tournament.PrizePool)), "tournament.prize", $"tournament:prize:{tournament.Id}:{effect.VictorUserId}", ct);
        var updatedMatch = await MatchAsync(context, effect.MatchId, false, ct);
        var victor = await PlayerAsync(context, match.TournamentId, effect.VictorUserId, false, ct);
        await AppendHistoryAsync(context, finished ? "tournament.finished" : "tournament.match_reported", tournament.SeasonId, tournament.ChatId, effect.VictorUserId, tournament.Id.ToString(CultureInfo.InvariantCulture), new { effect.MatchId, effect.VictorUserId, finished }, ct);
        context.SetOutput("result", new TournamentReportResult(true, finished, finished ? "Турнир завершён." : "Матч засчитан, игрок продвинут дальше.", updatedMatch, victor));
    }
}
