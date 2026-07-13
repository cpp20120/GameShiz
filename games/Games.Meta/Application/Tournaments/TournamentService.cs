
using System.Globalization;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Games.Meta.Application.Effects;

namespace Games.Meta.Application.Tournaments;

public sealed class TournamentService(
    IMetaService meta,
    ITournamentStore tournaments,
    IAtomicEffectExecutor effects) : ITournamentService
{
    public async Task<TournamentCreateResult> CreateAsync(long chatId, long userId, string gameKey, int entryFee, int maxPlayers, CancellationToken ct)
    {
        var season = await meta.GetActiveSeasonAsync(ct);
        return await effects.ExecuteAsync(
            new AtomicEffectExecutionEnvelope(
                "meta.tournament",
                $"meta:tournament:create:{season.Id}:{chatId}:{userId}:{Guid.NewGuid():N}",
                $"{season.Id}:{chatId}",
                [$"game:meta.tournament:{season.Id}:{chatId}"]),
            new AtomicEffectPlan<TournamentCreateResult>(
                new(false, "Турнир не создан."),
                [new TournamentCreateAtomicEffect(season.Id, chatId, userId, gameKey, entryFee, maxPlayers)],
                outputs => (TournamentCreateResult)outputs["result"]!),
            ct).ConfigureAwait(false);
    }

    public async Task<TournamentJoinResult> JoinAsync(long tournamentId, long userId, long chatId, string displayName, CancellationToken ct)
    {
        return await effects.ExecuteAsync(
            new AtomicEffectExecutionEnvelope(
                "meta.tournament",
                $"meta:tournament:join:{tournamentId}:{chatId}:{userId}",
                tournamentId.ToString(CultureInfo.InvariantCulture),
                [$"game:meta.tournament:{tournamentId}", $"wallet:{chatId}:{userId}"]),
            new AtomicEffectPlan<TournamentJoinResult>(
                new(false, "Турнир не найден."),
                [new TournamentJoinAtomicEffect(tournamentId, userId, chatId, displayName)],
                outputs => (TournamentJoinResult)outputs["result"]!),
            ct).ConfigureAwait(false);
    }

    public Task<TournamentInfo?> GetAsync(long tournamentId, CancellationToken ct) =>
        tournaments.GetAsync(tournamentId, ct);

    public async Task<IReadOnlyList<TournamentInfo>> GetOpenAsync(long chatId, int limit, CancellationToken ct)
    {
        var season = await meta.GetActiveSeasonAsync(ct);
        return await tournaments.GetOpenAsync(season, chatId, limit, ct);
    }

    public Task<IReadOnlyList<TournamentPlayerInfo>> GetPlayersAsync(long tournamentId, CancellationToken ct) =>
        tournaments.GetPlayersAsync(tournamentId, ct);

    public Task<IReadOnlyList<TournamentMatchInfo>> GetMatchesAsync(long tournamentId, CancellationToken ct) =>
        tournaments.GetMatchesAsync(tournamentId, ct);

    public async Task<bool> StartAsync(long tournamentId, long userId, CancellationToken ct)
    {
        return await effects.ExecuteAsync(
            new AtomicEffectExecutionEnvelope("meta.tournament", $"meta:tournament:start:{tournamentId}", tournamentId.ToString(CultureInfo.InvariantCulture), [$"game:meta.tournament:{tournamentId}"]),
            new AtomicEffectPlan<bool>(false, [new TournamentStartAtomicEffect(tournamentId, userId)], outputs => (bool)outputs["result"]!),
            ct).ConfigureAwait(false);
    }

    public async Task<TournamentReportResult> ReportMatchAsync(long matchId, long actorUserId, long victorUserId, CancellationToken ct)
    {
        var match = await tournaments.GetMatchAsync(matchId, ct).ConfigureAwait(false);
        var tournament = match is null ? null : await tournaments.GetAsync(match.TournamentId, ct).ConfigureAwait(false);
        var lockKeys = new List<string>
        {
            $"game:meta.tournament:match:{matchId}",
        };
        if (tournament is not null)
        {
            lockKeys.Add($"game:meta.tournament:{tournament.Id}");
            lockKeys.Add($"wallet:{tournament.ChatId}:{victorUserId}");
        }
        return await effects.ExecuteAsync(
            new AtomicEffectExecutionEnvelope("meta.tournament", $"meta:tournament:report:{matchId}:{victorUserId}", $"match:{matchId}", lockKeys),
            new AtomicEffectPlan<TournamentReportResult>(new(false, false, "Матч не найден."), [new TournamentReportAtomicEffect(matchId, actorUserId, victorUserId)], outputs => (TournamentReportResult)outputs["result"]!),
            ct).ConfigureAwait(false);
    }

    public async Task<TournamentPlayerInfo?> FinishAsync(long tournamentId, long actorUserId, long victorUserId, CancellationToken ct)
    {
        var before = await tournaments.GetAsync(tournamentId, ct).ConfigureAwait(false);
        var lockKeys = new List<string> { $"game:meta.tournament:{tournamentId}" };
        if (before is not null)
            lockKeys.Add($"wallet:{before.ChatId}:{victorUserId}");
        return await effects.ExecuteAsync(
            new AtomicEffectExecutionEnvelope("meta.tournament", $"meta:tournament:finish:{tournamentId}:{victorUserId}", tournamentId.ToString(CultureInfo.InvariantCulture), lockKeys),
            new AtomicEffectPlan<TournamentPlayerInfo?>(null, [new TournamentFinishAtomicEffect(tournamentId, actorUserId, victorUserId)], outputs => (TournamentPlayerInfo?)outputs["result"]!),
            ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TournamentPlayerInfo>?> CancelAsync(long tournamentId, long actorUserId, CancellationToken ct)
    {
        var before = await tournaments.GetAsync(tournamentId, ct).ConfigureAwait(false);
        var lockKeys = new List<string> { $"game:meta.tournament:{tournamentId}" };
        if (before is not null)
        {
            var players = await tournaments.GetPlayersAsync(tournamentId, ct).ConfigureAwait(false);
            lockKeys.AddRange(players
                .Where(static player => string.Equals(player.Status, "joined", StringComparison.Ordinal))
                .Select(player => $"wallet:{before.ChatId}:{player.UserId}"));
        }
        return await effects.ExecuteAsync(
            new AtomicEffectExecutionEnvelope("meta.tournament", $"meta:tournament:cancel:{tournamentId}", tournamentId.ToString(CultureInfo.InvariantCulture), lockKeys),
            new AtomicEffectPlan<IReadOnlyList<TournamentPlayerInfo>?>(null, [new TournamentCancelAtomicEffect(tournamentId, actorUserId)], outputs => (IReadOnlyList<TournamentPlayerInfo>?)outputs["result"]!),
            ct).ConfigureAwait(false);
    }
}
