using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Games.Meta.Application.Effects;
using Games.Meta.Application.Meta;
using Games.Meta.Application.Tournaments;
using Games.Meta.Domain.Seasons;
using Games.Meta.Domain.Tournaments;
using Games.Meta.Infrastructure.Persistence;

namespace CasinoShiz.Tests;

public sealed class StatefulTournamentPropertyTests
{
    private static readonly MetaSeason Season = new(
        7,
        "PBT season",
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch.AddDays(14),
        "active",
        "{}");

    [Property(MaxTest = 100)]
    public async Task<Property> Tournament_CommandSequence_PreservesLifecycleInvariants(NonEmptyArray<int> commands)
    {
        var store = new ModelTournamentStore();
        var executor = new TournamentCommandExecutor(
            new ModelMetaService(Season),
            store,
            new ModelTournamentEffects(store));
        string? failure = null;

        foreach (var rawCommand in commands.Get)
        {
            var magnitude = Math.Abs((long)rawCommand);
            switch (magnitude % 7)
            {
                case 0:
                    await executor.CreateAsync(100, 1, "dice", 10, 4, $"create:{magnitude}:{rawCommand}", default);
                    break;
                case 1:
                case 2:
                    var userId = 2 + magnitude % 8;
                    await executor.JoinAsync(1, userId, 100, $"user-{userId}", $"join:{magnitude}", default);
                    break;
                case 3:
                    await executor.StartAsync(1, 1, $"start:{magnitude}", default);
                    break;
                case 4:
                    var players = await store.GetPlayersAsync(1, default);
                    var victor = players.FirstOrDefault()?.UserId ?? 2;
                    await executor.ReportMatchAsync(1, 1, victor, $"report:{magnitude}", default);
                    break;
                case 5:
                    var finishPlayers = await store.GetPlayersAsync(1, default);
                    var winner = finishPlayers.FirstOrDefault()?.UserId ?? 2;
                    await executor.FinishAsync(1, 1, winner, $"finish:{magnitude}", default);
                    break;
                default:
                    await executor.CancelAsync(1, 1, $"cancel:{magnitude}", default);
                    break;
            }

            failure = store.CheckInvariants();
            if (failure is not null)
                break;
        }

        return ((failure ?? store.CheckInvariants()) is null)
            .ToProperty()
            .Label(failure ?? $"commands={commands.Get.Length}, state={store.Describe()}");
    }

    private sealed class ModelMetaService(MetaSeason season) : IMetaService
    {
        public Task<MetaSeason> GetActiveSeasonAsync(CancellationToken ct) => Task.FromResult(season);
        public Task<SeasonProfile> GetProfileAsync(long chatId, long userId, string displayName, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<SeasonLeaderboardEntry>> GetTopAsync(long chatId, int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlayerAchievementView>> GetAchievementsAsync(long chatId, long userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<GameStreakRecordResult?> RecordGamePlayedAsync(long seasonId, long chatId, long userId, string gameKey, DateOnly playedOn, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlayerGameStreakView>> GetGameStreaksAsync(long chatId, long userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<SeasonPlayer> ApplyGameCompletedAsync(long chatId, long userId, string displayName, long stake, long payout, bool isWin, CancellationToken ct) => throw new NotSupportedException();
        public Task<SeasonPlayer> AddSeasonXpAsync(long seasonId, long chatId, long userId, string displayName, long xpDelta, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<AchievementUnlock>> UnlockAchievementsAsync(long seasonId, long chatId, long userId, IEnumerable<AchievementDefinition> achievements, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class ModelTournamentEffects(ModelTournamentStore store) : IAtomicEffectExecutor
    {
        public async Task<TResult> ExecuteAsync<TResult>(
            AtomicEffectExecutionEnvelope envelope,
            AtomicEffectPlan<TResult> plan,
            CancellationToken ct)
        {
            object? result = plan.Effects.Single() switch
            {
                TournamentCreateAtomicEffect effect => await store.CreateAsync(
                    new MetaSeason(7, "PBT season", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(14), "active", "{}"),
                    effect.ChatId,
                    effect.CreatedBy,
                    effect.GameKey,
                    effect.EntryFee,
                    effect.MaxPlayers,
                    ct),
                TournamentJoinAtomicEffect effect => await store.JoinAsync(effect.TournamentId, effect.UserId, effect.DisplayName, ct),
                TournamentStartAtomicEffect effect => await store.StartAsync(effect.TournamentId, effect.UserId, ct),
                TournamentReportAtomicEffect effect => await store.ReportMatchAsync(effect.MatchId, effect.ActorUserId, effect.VictorUserId, ct),
                TournamentFinishAtomicEffect effect => await store.FinishAsync(effect.TournamentId, effect.ActorUserId, effect.VictorUserId, ct),
                TournamentCancelAtomicEffect effect => await store.CancelAsync(effect.TournamentId, effect.ActorUserId, ct),
                _ => throw new InvalidOperationException("Unexpected tournament effect."),
            };

            if (plan.ResultFactory is null)
                return plan.Result;
            return plan.ResultFactory(new Dictionary<string, object?> { ["result"] = result });
        }
    }

    private sealed class ModelTournamentStore : ITournamentStore
    {
        private readonly List<TournamentPlayerInfo> players = [];
        private readonly List<TournamentMatchInfo> matches = [];
        private TournamentInfo? tournament;

        public Task<TournamentCreateResult> CreateAsync(MetaSeason season, long chatId, long createdBy, string gameKey, int entryFee, int maxPlayers, CancellationToken ct)
        {
            if (tournament is not null)
                return Task.FromResult(new TournamentCreateResult(false, "already exists", tournament));
            if (entryFee < 0 || maxPlayers < 2)
                return Task.FromResult(new TournamentCreateResult(false, "invalid configuration"));

            tournament = new TournamentInfo(1, season.Id, chatId, gameKey, "single_elim", "open", entryFee, maxPlayers, createdBy, DateTimeOffset.UnixEpoch, 0, 0);
            return Task.FromResult(new TournamentCreateResult(true, "created", tournament));
        }

        public Task<TournamentJoinResult> JoinAsync(long tournamentId, long userId, string displayName, CancellationToken ct)
        {
            if (tournament is not { } current || current.Id != tournamentId)
                return Task.FromResult(new TournamentJoinResult(false, "not found"));
            if (current.Status != "open")
                return Task.FromResult(new TournamentJoinResult(false, "not open", current));
            if (players.Any(player => player.UserId == userId))
                return Task.FromResult(new TournamentJoinResult(false, "already joined", current));
            if (players.Count >= current.MaxPlayers)
                return Task.FromResult(new TournamentJoinResult(false, "full", current));

            players.Add(new TournamentPlayerInfo(current.Id, userId, displayName, "joined", DateTimeOffset.UnixEpoch));
            tournament = current with { PlayerCount = players.Count, PrizePool = checked((long)players.Count * current.EntryFee) };
            return Task.FromResult(new TournamentJoinResult(true, "joined", tournament));
        }

        public Task<TournamentInfo?> GetAsync(long tournamentId, CancellationToken ct) =>
            Task.FromResult(tournament?.Id == tournamentId ? tournament : null);

        public Task<IReadOnlyList<TournamentInfo>> GetOpenAsync(MetaSeason season, long chatId, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TournamentInfo>>(tournament is { Status: "open", ChatId: var id } current && id == chatId ? [current] : []);

        public Task<IReadOnlyList<TournamentPlayerInfo>> GetPlayersAsync(long tournamentId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TournamentPlayerInfo>>(tournament?.Id == tournamentId ? players.ToArray() : []);

        public Task<IReadOnlyList<TournamentMatchInfo>> GetMatchesAsync(long tournamentId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TournamentMatchInfo>>(tournament?.Id == tournamentId ? matches.ToArray() : []);

        public Task<TournamentMatchInfo?> GetMatchAsync(long matchId, CancellationToken ct) =>
            Task.FromResult(matches.FirstOrDefault(match => match.Id == matchId));

        public Task<bool> StartAsync(long tournamentId, long userId, CancellationToken ct)
        {
            if (tournament is not { } current || current.Id != tournamentId || current.Status != "open" || current.CreatedBy != userId || players.Count < 2)
                return Task.FromResult(false);

            tournament = current with { Status = "started" };
            var first = players[0];
            var second = players[1];
            matches.Add(new TournamentMatchInfo(1, current.Id, 1, 1, "ready", first.UserId, first.DisplayName, second.UserId, second.DisplayName, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch));
            return Task.FromResult(true);
        }

        public Task<TournamentReportResult> ReportMatchAsync(long matchId, long actorUserId, long victorUserId, CancellationToken ct)
        {
            if (tournament is not { } current || current.Status != "started")
                return Task.FromResult(new TournamentReportResult(false, false, "not started"));
            var match = matches.FirstOrDefault(candidate => candidate.Id == matchId);
            if (match is null || match.Status != "ready" || current.CreatedBy != actorUserId
                || (match.Player1UserId != victorUserId && match.Player2UserId != victorUserId))
                return Task.FromResult(new TournamentReportResult(false, false, "invalid report", match));

            var updatedMatch = match with { Status = "finished", VictorUserId = victorUserId };
            matches[0] = updatedMatch;
            for (var index = 0; index < players.Count; index++)
            {
                var player = players[index];
                players[index] = player with { Status = player.UserId == victorUserId ? "winner" : "loser" };
            }
            tournament = current with { Status = "finished" };
            var victor = players.Single(player => player.UserId == victorUserId);
            return Task.FromResult(new TournamentReportResult(true, true, "reported", updatedMatch, victor));
        }

        public Task<TournamentPlayerInfo?> FinishAsync(long tournamentId, long actorUserId, long winnerUserId, CancellationToken ct)
        {
            if (tournament is not { } current || current.Id != tournamentId || current.Status != "started" || current.CreatedBy != actorUserId)
                return Task.FromResult<TournamentPlayerInfo?>(null);
            var player = players.FirstOrDefault(candidate => candidate.UserId == winnerUserId && candidate.Status == "joined");
            if (player is null)
                return Task.FromResult<TournamentPlayerInfo?>(null);
            var winner = player with { Status = "winner" };
            for (var index = 0; index < players.Count; index++)
            {
                var candidate = players[index];
                players[index] = candidate.UserId == winnerUserId
                    ? winner
                    : candidate with { Status = candidate.Status == "joined" ? "eliminated" : candidate.Status };
            }
            tournament = current with { Status = "finished" };
            return Task.FromResult<TournamentPlayerInfo?>(winner);
        }

        public Task<IReadOnlyList<TournamentPlayerInfo>?> CancelAsync(long tournamentId, long actorUserId, CancellationToken ct)
        {
            if (tournament is not { } current || current.Id != tournamentId || current.CreatedBy != actorUserId || current.Status is not ("open" or "started"))
                return Task.FromResult<IReadOnlyList<TournamentPlayerInfo>?>(null);
            for (var index = 0; index < players.Count; index++)
                players[index] = players[index] with { Status = "refunded" };
            tournament = current with { Status = "cancelled" };
            return Task.FromResult<IReadOnlyList<TournamentPlayerInfo>?>(players.ToArray());
        }

        public string? CheckInvariants()
        {
            if (tournament is null)
                return players.Count == 0 && matches.Count == 0 ? null : "tournament model has orphan state";
            if (tournament.PlayerCount != players.Count || tournament.PrizePool != (long)players.Count * tournament.EntryFee)
                return "tournament player count or prize pool is inconsistent";
            if (players.Select(player => player.UserId).Distinct().Count() != players.Count)
                return "tournament contains duplicate players";
            if (matches.Any(match => match.TournamentId != tournament.Id))
                return "tournament contains a foreign match";
            if (tournament.Status == "open" && matches.Count != 0)
                return "open tournament already has matches";
            if (tournament.Status == "started" && (players.Count < 2 || matches.Count != 1 || matches[0].Status != "ready"))
                return "started tournament does not have one ready match";
            if (tournament.Status == "finished"
                && (players.Count(player => player.Status == "winner") != 1
                    || players.Any(player => player.Status == "joined")))
                return "finished tournament has no unique winner";
            if (tournament.Status == "cancelled" && players.Any(player => player.Status == "joined"))
                return "cancelled tournament still has joined players";
            return null;
        }

        public string Describe() => tournament is null ? "empty" : $"{tournament.Status}, players={players.Count}, matches={matches.Count}";
    }
}
