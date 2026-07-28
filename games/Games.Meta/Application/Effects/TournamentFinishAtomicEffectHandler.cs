using System.Globalization;
using System.Text.Json;
using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Execution;
using Dapper;

namespace Games.Meta.Application.Effects;

internal sealed class TournamentFinishAtomicEffectHandler : TournamentAtomicEffectHandler<TournamentFinishAtomicEffect>
{
    protected override async Task ApplyAsync(TournamentFinishAtomicEffect effect, IAtomicEffectContext context, CancellationToken ct)
    {
        var tournament = await TournamentAsync(context, effect.TournamentId, true, ct);
        var player = await PlayerAsync(context, effect.TournamentId, effect.VictorUserId, true, ct);
        if (tournament is null || player is null || tournament.CreatedBy != effect.ActorUserId || !string.Equals(tournament.Status, "started", StringComparison.Ordinal) || !string.Equals(player.Status, "joined", StringComparison.Ordinal))
        { context.SetOutput("result", null); return; }
        await CompleteTournamentAsync(context, effect.TournamentId, effect.VictorUserId, ct);
        if (tournament.PrizePool > 0 && !effect.PrizeAlreadyPaid)
            await CreditAsync(context, effect.VictorUserId, tournament.ChatId, player.DisplayName, checked((int)Math.Min(int.MaxValue, tournament.PrizePool)), "tournament.prize", $"tournament:prize:{tournament.Id}:{effect.VictorUserId}", ct);
        await AppendHistoryAsync(context, "tournament.finished", tournament.SeasonId, tournament.ChatId, effect.VictorUserId, tournament.Id.ToString(CultureInfo.InvariantCulture), new { effect.TournamentId, effect.VictorUserId, tournament.PrizePool, via = "manual" }, ct);
        context.SetOutput("result", player with { Status = "winner" });
    }
}
