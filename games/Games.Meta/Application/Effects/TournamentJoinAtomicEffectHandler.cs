using System.Globalization;
using System.Text.Json;
using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Execution;
using Dapper;

namespace Games.Meta.Application.Effects;

internal sealed class TournamentJoinAtomicEffectHandler : TournamentAtomicEffectHandler<TournamentJoinAtomicEffect>
{
    protected override async Task ApplyAsync(TournamentJoinAtomicEffect effect, IAtomicEffectContext context, CancellationToken ct)
    {
        var tournament = await TournamentAsync(context, effect.TournamentId, true, ct);
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
            new { effect.TournamentId, effect.UserId }, ct);
        if (exists > 0)
        {
            context.SetOutput("result", new TournamentJoinResult(false, "Ты уже зарегистрирован в этом турнире.", tournament));
            return;
        }

        if (!effect.WalletAlreadyApplied)
        {
            var wallet = context.Wallet ?? throw new InvalidOperationException("Wallet boundary is not configured.");
            await wallet.EnsureUserAsync(effect.UserId, effect.ChatId, effect.DisplayName, ct);
            if (!await TryDebitAsync(context, effect.UserId, effect.ChatId, tournament.EntryFee, "tournament.entry_fee", $"tournament:entry:{tournament.Id}:{effect.ChatId}:{effect.UserId}", ct))
            {
                context.SetOutput("result", new TournamentJoinResult(false, "Недостаточно монет для entry fee.", tournament));
                return;
            }
        }

        await context.ExecuteAsync(
            "INSERT INTO meta_tournament_players (tournament_id, user_id, display_name) VALUES (@tournamentId, @userId, @displayName)",
            new { effect.TournamentId, effect.UserId, effect.DisplayName }, ct);
        var updated = await TournamentAsync(context, effect.TournamentId, false, ct);
        await AppendHistoryAsync(context, "tournament.joined", tournament.SeasonId, effect.ChatId, effect.UserId, tournament.Id.ToString(CultureInfo.InvariantCulture), new { effect.TournamentId, effect.DisplayName, tournament.EntryFee }, ct);
        context.SetOutput("result", new TournamentJoinResult(true, "Ты зарегистрирован в турнире.", updated));
    }
}
