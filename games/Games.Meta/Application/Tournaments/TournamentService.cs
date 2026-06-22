using BotFramework.Host;

namespace Games.Meta.Application.Tournaments;

public sealed class TournamentService(
    IMetaService meta,
    ITournamentStore tournaments,
    IEconomicsService economics,
    IMetaHistoryStore history) : ITournamentService
{
    public async Task<TournamentCreateResult> CreateAsync(long chatId, long userId, string gameKey, int entryFee, int maxPlayers, CancellationToken ct)
    {
        var season = await meta.GetActiveSeasonAsync(ct);
        var result = await tournaments.CreateAsync(season, chatId, userId, gameKey, entryFee, maxPlayers, ct);
        if (result.Created && result.Tournament is not null)
        {
            await history.AppendAsync("tournament.created", "tournament", result.Tournament.Id.ToString(), season.Id, chatId, userId, new
            {
                result.Tournament.Id,
                result.Tournament.GameKey,
                result.Tournament.EntryFee,
                result.Tournament.MaxPlayers,
                result.Tournament.CreatedBy,
            }, ct);
        }
        return result;
    }

    public async Task<TournamentJoinResult> JoinAsync(long tournamentId, long userId, long chatId, string displayName, CancellationToken ct)
    {
        var tournament = await tournaments.GetAsync(tournamentId, ct);
        if (tournament is null) return new TournamentJoinResult(false, "Турнир не найден.");
        if (tournament.ChatId != chatId) return new TournamentJoinResult(false, "Этот турнир создан в другом чате.");

        await economics.EnsureUserAsync(userId, chatId, displayName, ct);
        if (tournament.EntryFee > 0)
        {
            var debit = await economics.TryDebitOnceAsync(
                userId,
                chatId,
                tournament.EntryFee,
                "tournament.entry_fee",
                $"tournament:entry:{tournament.Id}:{chatId}:{userId}",
                ct);
            if (debit.Rejected)
                return new TournamentJoinResult(false, "Недостаточно монет для entry fee.", tournament);
        }

        var joined = await tournaments.JoinAsync(tournamentId, userId, displayName, ct);
        if (joined.Joined)
        {
            await history.AppendAsync("tournament.joined", "tournament", tournamentId.ToString(), tournament.SeasonId, chatId, userId, new
            {
                tournamentId,
                displayName,
                tournament.EntryFee,
                joined.Tournament?.PlayerCount,
                joined.Tournament?.MaxPlayers,
            }, ct);
        }
        else if (tournament.EntryFee > 0)
        {
            await economics.CreditOnceAsync(
                userId,
                chatId,
                tournament.EntryFee,
                "tournament.entry_fee.refund",
                $"tournament:entry-refund:{tournament.Id}:{chatId}:{userId}",
                ct);
        }

        return joined;
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
        var before = await tournaments.GetAsync(tournamentId, ct);
        var started = await tournaments.StartAsync(tournamentId, userId, ct);
        if (started && before is not null)
        {
            var matches = await tournaments.GetMatchesAsync(tournamentId, ct);
            await history.AppendAsync("tournament.started", "tournament", tournamentId.ToString(), before.SeasonId, before.ChatId, userId, new
            {
                tournamentId,
                before.GameKey,
                before.PlayerCount,
                before.MaxPlayers,
                matchCount = matches.Count,
                readyMatches = matches.Count(x => x.Status == "ready"),
                byes = matches.Count(x => x.Status == "byed"),
            }, ct);
        }
        return started;
    }

    public async Task<TournamentReportResult> ReportMatchAsync(long matchId, long actorUserId, long victorUserId, CancellationToken ct)
    {
        var result = await tournaments.ReportMatchAsync(matchId, actorUserId, victorUserId, ct);
        if (!result.Updated || result.Match is null) return result;

        var tournament = await tournaments.GetAsync(result.Match.TournamentId, ct);
        if (tournament is not null)
        {
            await history.AppendAsync("tournament.match_reported", "tournament", tournament.Id.ToString(), tournament.SeasonId, tournament.ChatId, victorUserId, new
            {
                matchId,
                result.Match.Round,
                result.Match.MatchIndex,
                result.Match.Player1UserId,
                result.Match.Player2UserId,
                result.Match.VictorUserId,
                result.Finished,
            }, ct);
        }

        if (!result.Finished || result.Victor is null || tournament is null) return result;

        if (tournament.PrizePool > 0)
        {
            var amount = (int)Math.Min(int.MaxValue, tournament.PrizePool);
            await economics.CreditOnceAsync(
                result.Victor.UserId,
                tournament.ChatId,
                amount,
                "tournament.prize",
                $"tournament:prize:{tournament.Id}:{result.Victor.UserId}",
                ct);
        }

        await history.AppendAsync("tournament.finished", "tournament", tournament.Id.ToString(), tournament.SeasonId, tournament.ChatId, result.Victor.UserId, new
        {
            tournament.Id,
            victorUserId = result.Victor.UserId,
            result.Victor.DisplayName,
            tournament.PrizePool,
            via = "bracket",
        }, ct);

        return result;
    }

    public async Task<TournamentPlayerInfo?> FinishAsync(long tournamentId, long actorUserId, long victorUserId, CancellationToken ct)
    {
        var before = await tournaments.GetAsync(tournamentId, ct);
        if (before is null) return null;

        var victor = await tournaments.FinishAsync(tournamentId, actorUserId, victorUserId, ct);
        if (victor is null) return null;

        if (before.PrizePool > 0)
        {
            var amount = (int)Math.Min(int.MaxValue, before.PrizePool);
            await economics.CreditOnceAsync(
                victor.UserId,
                before.ChatId,
                amount,
                "tournament.prize",
                $"tournament:prize:{before.Id}:{victor.UserId}",
                ct);
        }

        await history.AppendAsync("tournament.finished", "tournament", before.Id.ToString(), before.SeasonId, before.ChatId, victor.UserId, new
        {
            before.Id,
            victorUserId = victor.UserId,
            victor.DisplayName,
            before.PrizePool,
            via = "manual",
        }, ct);

        return victor;
    }

    public async Task<IReadOnlyList<TournamentPlayerInfo>?> CancelAsync(long tournamentId, long actorUserId, CancellationToken ct)
    {
        var before = await tournaments.GetAsync(tournamentId, ct);
        if (before is null) return null;

        var players = await tournaments.CancelAsync(tournamentId, actorUserId, ct);
        if (players is null) return null;

        if (before.EntryFee > 0)
        {
            foreach (var player in players)
            {
                await economics.CreditOnceAsync(
                    player.UserId,
                    before.ChatId,
                    before.EntryFee,
                    "tournament.cancel.refund",
                    $"tournament:cancel-refund:{before.Id}:{player.UserId}",
                    ct);
            }
        }

        await history.AppendAsync("tournament.cancelled", "tournament", before.Id.ToString(), before.SeasonId, before.ChatId, actorUserId, new
        {
            before.Id,
            before.EntryFee,
            refundedPlayers = players.Select(x => new { x.UserId, x.DisplayName }).ToArray(),
        }, ct);

        return players;
    }
}
