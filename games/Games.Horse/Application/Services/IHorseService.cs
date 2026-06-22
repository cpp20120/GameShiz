// ─────────────────────────────────────────────────────────────────────────────
// HorseService — daily horse-race betting pool. Ported from
// src/CasinoShiz.Core/Services/Horse/HorseService.cs:
//
//   • EF Core HorseBet/HorseResult rows → IHorseBetStore / IHorseResultStore.
//   • EconomicsService.Debit/Credit now take userId, not an entity.
//   • BotOptions.Admins gate replaced by per-module HorseOptions.Admins —
//     modules own their own access policy.
//
// Pool math is identical: each horse's koef = (pot - stake_on_horse) /
// (1.1 * stake_on_horse) + 1, floored to 3 decimals. The winning bettor's
// payout = bet * koef (integer-floored).
// ─────────────────────────────────────────────────────────────────────────────

using System.Security.Cryptography;
using System.Text;
using BotFramework.Host;
using BotFramework.Sdk;
using Games.Horse.Generators;
using Microsoft.Extensions.Options;
using static Games.Horse.HorseResultHelpers;

namespace Games.Horse;

public interface IHorseService
{
    Task<BetResult> PlaceBetAsync(
        long userId, string displayName, long balanceScopeId, int horseId, int amount, CancellationToken ct);

    Task<BetResult> PlaceBetAsync(
        long userId, string displayName, long balanceScopeId, int horseId, int amount, int sourceMessageId, CancellationToken ct);

    /// <param name="balanceScopeIdOnly">If null, aggregate every chat (admin). Else this Telegram chat only.</param>
    Task<RaceInfo> GetTodayInfoAsync(long? balanceScopeIdOnly, CancellationToken ct);

    /// <summary>Local result for this chat, else today's global result (scope 0).</summary>
    Task<TodayRaceResult> GetTodayResultAsync(long viewerBalanceScopeId, CancellationToken ct);

    Task<RaceOutcome> RunRaceAsync(
        long callerUserId, HorseRunKind kind, long chatScopeId, CancellationToken ct);

    Task SaveFileIdAsync(string raceDate, long balanceScopeId, string fileId, CancellationToken ct);
}
