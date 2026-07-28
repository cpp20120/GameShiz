using System.Globalization;
using System.Text.Json;
using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Execution;
using Dapper;

namespace Games.Meta.Application.Effects;

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
            new { effect.SeasonId, effect.ChatId, gameKey, effect.EntryFee, effect.MaxPlayers, effect.CreatedBy }, ct);
        if (id is null) throw new InvalidOperationException("Tournament creation did not return an id.");
        var tournament = await TournamentAsync(context, id.Value, false, ct);
        await AppendHistoryAsync(context, "tournament.created", effect.SeasonId, effect.ChatId, effect.CreatedBy, id.Value.ToString(CultureInfo.InvariantCulture), new { id, gameKey, effect.EntryFee, effect.MaxPlayers }, ct);
        context.SetOutput("result", new TournamentCreateResult(true, "Турнир создан.", tournament));
    }
}
