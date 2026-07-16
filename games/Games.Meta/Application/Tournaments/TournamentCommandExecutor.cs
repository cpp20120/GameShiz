using System.Globalization;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Games.Meta.Application.Effects;

namespace Games.Meta.Application.Tournaments;

/// <summary>
/// Executes one tournament transition. In production the workflow wallet is
/// used outside the Backend transaction: wallet steps are idempotent, the
/// local AtomicEffect commits separately, and a rejected local transition is
/// compensated. The null-wallet path preserves the old monolith test seam.
/// </summary>
public sealed class TournamentCommandExecutor(
    IMetaService meta,
    ITournamentStore tournaments,
    IAtomicEffectExecutor effects,
    IWalletAtomicExecutionService? workflowWallet = null)
{
    public async Task<TournamentCreateResult> CreateAsync(
        long chatId,
        long userId,
        string gameKey,
        int entryFee,
        int maxPlayers,
        string commandId,
        CancellationToken ct)
    {
        var season = await meta.GetActiveSeasonAsync(ct).ConfigureAwait(false);
        return await effects.ExecuteAsync(
            new AtomicEffectExecutionEnvelope(
                "meta.tournament",
                commandId,
                $"{season.Id}:{chatId}",
                [$"game:meta.tournament:{season.Id}:{chatId}" ]),
            new AtomicEffectPlan<TournamentCreateResult>(
                new(false, "Турнир не создан."),
                [new TournamentCreateAtomicEffect(season.Id, chatId, userId, gameKey, entryFee, maxPlayers)],
                outputs => (TournamentCreateResult)outputs["result"]!),
            ct).ConfigureAwait(false);
    }

    public async Task<TournamentJoinResult> JoinAsync(
        long tournamentId,
        long userId,
        long chatId,
        string displayName,
        string commandId,
        CancellationToken ct)
    {
        if (workflowWallet is null)
            return await ExecuteLegacyJoinAsync(tournamentId, userId, chatId, displayName, commandId, ct).ConfigureAwait(false);

        var before = await tournaments.GetAsync(tournamentId, ct).ConfigureAwait(false);
        if (before is null)
            return new TournamentJoinResult(false, "Турнир не найден.");
        if (before.ChatId != chatId)
            return new TournamentJoinResult(false, "Этот турнир создан в другом чате.");
        if (!string.Equals(before.Status, "open", StringComparison.Ordinal))
            return new TournamentJoinResult(false, "Турнир уже не открыт для регистрации.");
        if (before.PlayerCount >= before.MaxPlayers)
            return new TournamentJoinResult(false, "Турнир уже заполнен.", before);

        var players = await tournaments.GetPlayersAsync(tournamentId, ct).ConfigureAwait(false);
        if (players.Any(player => player.UserId == userId))
            return new TournamentJoinResult(false, "Ты уже зарегистрирован в этом турнире.", before);

        var walletApplied = false;
        if (before.EntryFee > 0)
        {
            await workflowWallet.EnsureUserAsync(userId, chatId, displayName, ct).ConfigureAwait(false);
            var debit = await workflowWallet.ApplyBatchAsync(
                userId,
                chatId,
                [new WalletBatchEffect(WalletBatchEffectKind.Debit, before.EntryFee, "tournament.entry_fee")],
                $"tournament:workflow:join:debit:{commandId}",
                ct).ConfigureAwait(false);
            if (debit.Rejected)
                return new TournamentJoinResult(false, "Недостаточно монет для entry fee.", before);
            walletApplied = debit.Applied;
        }

        var result = await effects.ExecuteAsync(
            new AtomicEffectExecutionEnvelope(
                "meta.tournament",
                commandId,
                tournamentId.ToString(CultureInfo.InvariantCulture),
                [$"game:meta.tournament:{tournamentId}", $"wallet:{chatId}:{userId}" ]),
            new AtomicEffectPlan<TournamentJoinResult>(
                new(false, "Турнир не найден."),
                [new TournamentJoinAtomicEffect(tournamentId, userId, chatId, displayName, WalletAlreadyApplied: true)],
                outputs => (TournamentJoinResult)outputs["result"]!),
            ct).ConfigureAwait(false);

        if (result.Joined || !walletApplied)
            return result;

        await CompensateCreditAsync(
            workflowWallet,
            userId,
            chatId,
            displayName,
            before.EntryFee,
            "tournament.entry_fee.rollback",
            $"tournament:workflow:join:compensation:{commandId}",
            ct).ConfigureAwait(false);
        return result;
    }

    public Task<bool> StartAsync(
        long tournamentId,
        long userId,
        string commandId,
        CancellationToken ct) =>
        effects.ExecuteAsync(
            new AtomicEffectExecutionEnvelope(
                "meta.tournament",
                commandId,
                tournamentId.ToString(CultureInfo.InvariantCulture),
                [$"game:meta.tournament:{tournamentId}" ]),
            new AtomicEffectPlan<bool>(
                false,
                [new TournamentStartAtomicEffect(tournamentId, userId)],
                outputs => (bool)outputs["result"]!),
            ct);

    public async Task<TournamentReportResult> ReportMatchAsync(
        long matchId,
        long actorUserId,
        long victorUserId,
        string commandId,
        CancellationToken ct)
    {
        var match = await tournaments.GetMatchAsync(matchId, ct).ConfigureAwait(false);
        var tournament = match is null ? null : await tournaments.GetAsync(match.TournamentId, ct).ConfigureAwait(false);
        var lockKeys = new List<string> { $"game:meta.tournament:match:{matchId}" };
        if (tournament is not null)
        {
            lockKeys.Add($"game:meta.tournament:{tournament.Id}");
            lockKeys.Add($"wallet:{tournament.ChatId}:{victorUserId}");
        }

        if (workflowWallet is null)
            return await ExecuteLegacyReportAsync(matchId, actorUserId, victorUserId, commandId, lockKeys, ct).ConfigureAwait(false);

        var canReport = match is not null
            && tournament is not null
            && tournament.CreatedBy == actorUserId
            && string.Equals(tournament.Status, "started", StringComparison.Ordinal)
            && string.Equals(match.Status, "ready", StringComparison.Ordinal)
            && match.Player1UserId is not null
            && match.Player2UserId is not null
            && (victorUserId == match.Player1UserId || victorUserId == match.Player2UserId);
        var finalMatch = canReport && match is not null
            && await IsFinalMatchAsync(match, tournament!.Id, ct).ConfigureAwait(false);
        var prize = finalMatch && tournament is not null
            ? checked((int)Math.Min(int.MaxValue, tournament.PrizePool))
            : 0;
        var prizeApplied = false;

        if (prize > 0 && tournament is not null && match is not null)
        {
            var victorName = victorUserId == match.Player1UserId ? match.Player1DisplayName : match.Player2DisplayName;
            await workflowWallet.EnsureUserAsync(
                victorUserId,
                tournament.ChatId,
                victorName ?? victorUserId.ToString(CultureInfo.InvariantCulture),
                ct).ConfigureAwait(false);
            var payout = await workflowWallet.ApplyBatchAsync(
                victorUserId,
                tournament.ChatId,
                [new WalletBatchEffect(WalletBatchEffectKind.Credit, prize, "tournament.prize")],
                $"tournament:prize:{tournament.Id}:{victorUserId}",
                ct).ConfigureAwait(false);
            if (!payout.Applied)
                throw new InvalidOperationException("Tournament wallet rejected a prize credit.");
            prizeApplied = true;
        }

        var result = await effects.ExecuteAsync(
            new AtomicEffectExecutionEnvelope(
                "meta.tournament",
                commandId,
                $"match:{matchId}",
                lockKeys),
            new AtomicEffectPlan<TournamentReportResult>(
                new(false, false, "Матч не найден."),
                [new TournamentReportAtomicEffect(matchId, actorUserId, victorUserId, PrizeAlreadyPaid: true)],
                outputs => (TournamentReportResult)outputs["result"]!),
            ct).ConfigureAwait(false);

        if (result.Finished || !prizeApplied)
            return result;

        if (await IsTournamentFinishedForWinnerAsync(tournament!.Id, victorUserId, ct).ConfigureAwait(false))
            return result;

        await CompensateDebitAsync(
            workflowWallet,
            victorUserId,
            tournament.ChatId,
            prize,
            "tournament.prize.rollback",
            $"tournament:workflow:prize:compensation:{commandId}",
            ct).ConfigureAwait(false);
        return result;
    }

    public async Task<TournamentPlayerInfo?> FinishAsync(
        long tournamentId,
        long actorUserId,
        long victorUserId,
        string commandId,
        CancellationToken ct)
    {
        var before = await tournaments.GetAsync(tournamentId, ct).ConfigureAwait(false);
        var lockKeys = new List<string> { $"game:meta.tournament:{tournamentId}" };
        if (before is not null)
            lockKeys.Add($"wallet:{before.ChatId}:{victorUserId}");

        if (workflowWallet is null)
            return await ExecuteLegacyFinishAsync(tournamentId, actorUserId, victorUserId, commandId, lockKeys, ct).ConfigureAwait(false);
        if (before is null)
            return null;

        var player = (await tournaments.GetPlayersAsync(tournamentId, ct).ConfigureAwait(false))
            .FirstOrDefault(candidate => candidate.UserId == victorUserId);
        var canFinish = before.CreatedBy == actorUserId
            && string.Equals(before.Status, "started", StringComparison.Ordinal)
            && player is not null
            && string.Equals(player.Status, "joined", StringComparison.Ordinal);
        var prize = canFinish ? checked((int)Math.Min(int.MaxValue, before.PrizePool)) : 0;
        var prizeApplied = false;
        if (prize > 0 && player is not null)
        {
            await workflowWallet.EnsureUserAsync(victorUserId, before.ChatId, player.DisplayName, ct).ConfigureAwait(false);
            var payout = await workflowWallet.ApplyBatchAsync(
                victorUserId,
                before.ChatId,
                [new WalletBatchEffect(WalletBatchEffectKind.Credit, prize, "tournament.prize")],
                $"tournament:prize:{before.Id}:{victorUserId}",
                ct).ConfigureAwait(false);
            if (!payout.Applied)
                throw new InvalidOperationException("Tournament wallet rejected a prize credit.");
            prizeApplied = true;
        }

        var result = await effects.ExecuteAsync(
            new AtomicEffectExecutionEnvelope(
                "meta.tournament",
                commandId,
                tournamentId.ToString(CultureInfo.InvariantCulture),
                lockKeys),
            new AtomicEffectPlan<TournamentPlayerInfo?>(
                null,
                [new TournamentFinishAtomicEffect(tournamentId, actorUserId, victorUserId, PrizeAlreadyPaid: true)],
                outputs => (TournamentPlayerInfo?)outputs["result"]!),
            ct).ConfigureAwait(false);

        if (result is not null || !prizeApplied)
            return result;
        if (await IsTournamentFinishedForWinnerAsync(tournamentId, victorUserId, ct).ConfigureAwait(false))
            return result;

        await CompensateDebitAsync(
            workflowWallet,
            victorUserId,
            before.ChatId,
            prize,
            "tournament.prize.rollback",
            $"tournament:workflow:prize:compensation:{commandId}",
            ct).ConfigureAwait(false);
        return result;
    }

    public async Task<IReadOnlyList<TournamentPlayerInfo>?> CancelAsync(
        long tournamentId,
        long actorUserId,
        string commandId,
        CancellationToken ct)
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

        if (workflowWallet is null)
            return await ExecuteLegacyCancelAsync(tournamentId, actorUserId, commandId, lockKeys, ct).ConfigureAwait(false);
        if (before is null || before.CreatedBy != actorUserId || before.Status is not ("open" or "started"))
            return null;

        var playersToRefund = (await tournaments.GetPlayersAsync(tournamentId, ct).ConfigureAwait(false))
            .Where(player => string.Equals(player.Status, "joined", StringComparison.Ordinal))
            .ToArray();
        var refunded = new List<TournamentPlayerInfo>();
        if (before.EntryFee > 0)
        {
            foreach (var player in playersToRefund)
            {
                await workflowWallet.EnsureUserAsync(player.UserId, before.ChatId, player.DisplayName, ct).ConfigureAwait(false);
                var refund = await workflowWallet.ApplyBatchAsync(
                    player.UserId,
                    before.ChatId,
                    [new WalletBatchEffect(WalletBatchEffectKind.Credit, before.EntryFee, "tournament.cancel.refund")],
                    $"tournament:cancel-refund:{before.Id}:{player.UserId}",
                    ct).ConfigureAwait(false);
                if (!refund.Applied)
                    throw new InvalidOperationException($"Tournament wallet rejected a refund for player {player.UserId}.");
                refunded.Add(player);
            }
        }

        var result = await effects.ExecuteAsync(
            new AtomicEffectExecutionEnvelope(
                "meta.tournament",
                commandId,
                tournamentId.ToString(CultureInfo.InvariantCulture),
                lockKeys),
            new AtomicEffectPlan<IReadOnlyList<TournamentPlayerInfo>?>(
                null,
                [new TournamentCancelAtomicEffect(tournamentId, actorUserId, RefundsAlreadyPaid: true)],
                outputs => (IReadOnlyList<TournamentPlayerInfo>?)outputs["result"]!),
            ct).ConfigureAwait(false);

        if (result is not null)
        {
            if (before.EntryFee > 0)
            {
                var alreadyRefunded = refunded.Select(static player => player.UserId).ToHashSet();
                foreach (var player in result.Where(player => !alreadyRefunded.Contains(player.UserId)))
                    await CompensateCreditAsync(
                        workflowWallet,
                        player.UserId,
                        before.ChatId,
                        player.DisplayName,
                        before.EntryFee,
                        "tournament.cancel.refund",
                        $"tournament:cancel-refund:{before.Id}:{player.UserId}",
                        ct).ConfigureAwait(false);
            }
            return result;
        }
        if (refunded.Count == 0)
            return result;
        if (await IsTournamentCancelledAsync(tournamentId, ct).ConfigureAwait(false))
            return result;

        await CompensateRefundsAsync(workflowWallet, before.ChatId, refunded, before.EntryFee, commandId, ct).ConfigureAwait(false);
        return result;
    }

    private Task<TournamentJoinResult> ExecuteLegacyJoinAsync(long tournamentId, long userId, long chatId, string displayName, string commandId, CancellationToken ct) =>
        effects.ExecuteAsync(
            new AtomicEffectExecutionEnvelope(
                "meta.tournament",
                commandId,
                tournamentId.ToString(CultureInfo.InvariantCulture),
                [$"game:meta.tournament:{tournamentId}", $"wallet:{chatId}:{userId}" ]),
            new AtomicEffectPlan<TournamentJoinResult>(
                new(false, "Турнир не найден."),
                [new TournamentJoinAtomicEffect(tournamentId, userId, chatId, displayName)],
                outputs => (TournamentJoinResult)outputs["result"]!),
            ct);

    private Task<TournamentReportResult> ExecuteLegacyReportAsync(long matchId, long actorUserId, long victorUserId, string commandId, IReadOnlyList<string> lockKeys, CancellationToken ct) =>
        effects.ExecuteAsync(
            new AtomicEffectExecutionEnvelope("meta.tournament", commandId, $"match:{matchId}", lockKeys),
            new AtomicEffectPlan<TournamentReportResult>(
                new(false, false, "Матч не найден."),
                [new TournamentReportAtomicEffect(matchId, actorUserId, victorUserId)],
                outputs => (TournamentReportResult)outputs["result"]!),
            ct);

    private Task<TournamentPlayerInfo?> ExecuteLegacyFinishAsync(long tournamentId, long actorUserId, long victorUserId, string commandId, IReadOnlyList<string> lockKeys, CancellationToken ct) =>
        effects.ExecuteAsync(
            new AtomicEffectExecutionEnvelope("meta.tournament", commandId, tournamentId.ToString(CultureInfo.InvariantCulture), lockKeys),
            new AtomicEffectPlan<TournamentPlayerInfo?>(
                null,
                [new TournamentFinishAtomicEffect(tournamentId, actorUserId, victorUserId)],
                outputs => (TournamentPlayerInfo?)outputs["result"]!),
            ct);

    private Task<IReadOnlyList<TournamentPlayerInfo>?> ExecuteLegacyCancelAsync(long tournamentId, long actorUserId, string commandId, IReadOnlyList<string> lockKeys, CancellationToken ct) =>
        effects.ExecuteAsync(
            new AtomicEffectExecutionEnvelope("meta.tournament", commandId, tournamentId.ToString(CultureInfo.InvariantCulture), lockKeys),
            new AtomicEffectPlan<IReadOnlyList<TournamentPlayerInfo>?>(
                null,
                [new TournamentCancelAtomicEffect(tournamentId, actorUserId)],
                outputs => (IReadOnlyList<TournamentPlayerInfo>?)outputs["result"]!),
            ct);

    private async Task<bool> IsFinalMatchAsync(TournamentMatchInfo match, long tournamentId, CancellationToken ct)
    {
        var matches = await tournaments.GetMatchesAsync(tournamentId, ct).ConfigureAwait(false);
        return match.Round >= matches.Select(candidate => candidate.Round).DefaultIfEmpty(match.Round).Max();
    }

    private async Task<bool> IsTournamentFinishedForWinnerAsync(long tournamentId, long victorUserId, CancellationToken ct)
    {
        var tournament = await tournaments.GetAsync(tournamentId, ct).ConfigureAwait(false);
        if (tournament is null || !string.Equals(tournament.Status, "finished", StringComparison.Ordinal)) return false;
        var player = (await tournaments.GetPlayersAsync(tournamentId, ct).ConfigureAwait(false))
            .FirstOrDefault(candidate => candidate.UserId == victorUserId);
        return player is not null && string.Equals(player.Status, "winner", StringComparison.Ordinal);
    }

    private async Task<bool> IsTournamentCancelledAsync(long tournamentId, CancellationToken ct)
    {
        var tournament = await tournaments.GetAsync(tournamentId, ct).ConfigureAwait(false);
        return tournament is not null && string.Equals(tournament.Status, "cancelled", StringComparison.Ordinal);
    }

    private static async Task CompensateCreditAsync(
        IWalletAtomicExecutionService wallet,
        long userId,
        long chatId,
        string displayName,
        int amount,
        string reason,
        string operationId,
        CancellationToken ct)
    {
        if (amount <= 0) return;
        await wallet.EnsureUserAsync(userId, chatId, displayName, ct).ConfigureAwait(false);
        var result = await wallet.ApplyBatchAsync(
            userId,
            chatId,
            [new WalletBatchEffect(WalletBatchEffectKind.Credit, amount, reason)],
            operationId,
            ct).ConfigureAwait(false);
        if (!result.Applied)
            throw new InvalidOperationException("Tournament wallet compensation credit was rejected.");
    }

    private static async Task CompensateDebitAsync(
        IWalletAtomicExecutionService wallet,
        long userId,
        long chatId,
        int amount,
        string reason,
        string operationId,
        CancellationToken ct)
    {
        if (amount <= 0) return;
        var result = await wallet.ApplyBatchAsync(
            userId,
            chatId,
            [new WalletBatchEffect(WalletBatchEffectKind.Debit, amount, reason)],
            operationId,
            ct).ConfigureAwait(false);
        if (!result.Applied)
            throw new InvalidOperationException("Tournament wallet compensation debit was rejected.");
    }

    private static async Task CompensateRefundsAsync(
        IWalletAtomicExecutionService wallet,
        long chatId,
        IReadOnlyList<TournamentPlayerInfo> players,
        int amount,
        string commandId,
        CancellationToken ct)
    {
        foreach (var player in players)
            await CompensateDebitAsync(
                wallet,
                player.UserId,
                chatId,
                amount,
                "tournament.cancel.refund.rollback",
                $"tournament:workflow:cancel:compensation:{commandId}:{player.UserId}",
                ct).ConfigureAwait(false);
    }
}
