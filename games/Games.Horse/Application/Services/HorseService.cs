using System.Security.Cryptography;
using System.Text;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Games.Horse.Application.Execution;
using BotFramework.Rendering;
using Games.Horse.Rendering;
using Microsoft.Extensions.Options;

namespace Games.Horse.Application.Services;

/// <summary>Compatibility facade; bets and race settlement are committed by the atomic executor.</summary>
public sealed class HorseService(
    IHorseBetStore betStore,
    IHorseResultStore resultStore,
    IAtomicGameExecutor<HorsePlaceBetCommand, HorseBetState, BetResult> betExecutor,
    IAtomicGameExecutor<HorseRunCommand, HorseRaceState, RaceOutcome> runExecutor,
    IRenderQueue renders,
    IRenderHistory renderHistory,
    TimeProvider timeProvider,
    IOptions<HorseOptions> options) : IHorseService
{
    private readonly HorseOptions opts = options.Value;

    public int HorseCount => opts.HorseCount;
    public int MinBetsToRun => opts.MinBetsToRun;

    public Task<BetResult> PlaceBetAsync(
        long userId, string displayName, long balanceScopeId, int horseId, int amount,
        CancellationToken ct) =>
        PlaceBetAsync(userId, displayName, balanceScopeId, horseId, amount, 0, ct);

    public Task<BetResult> PlaceBetAsync(
        long userId, string displayName, long balanceScopeId, int horseId, int amount,
        int sourceMessageId, CancellationToken ct)
    {
        var raceDate = HorseTimeHelper.GetRaceDate(opts.TimezoneOffsetHours);
        var commandId = sourceMessageId != 0
            ? $"horse:bet:{balanceScopeId}:{sourceMessageId}:{userId}"
            : $"horse:bet:legacy:{Guid.NewGuid():N}";
        return betExecutor.ExecuteAsync(new(new HorsePlaceBetCommand(
            userId, displayName, balanceScopeId, horseId, amount, raceDate,
            StableGuid(commandId), commandId, opts.HorseCount)), ct);
    }

    public async Task<RaceInfo> GetTodayInfoAsync(long? balanceScopeIdOnly, CancellationToken ct)
    {
        var raceDate = HorseTimeHelper.GetRaceDate(opts.TimezoneOffsetHours);
        var bets = balanceScopeIdOnly is { } scope
            ? await betStore.ListByRaceDateAndScopeAsync(raceDate, scope, ct).ConfigureAwait(false)
            : await betStore.ListByRaceDateAsync(raceDate, ct).ConfigureAwait(false);
        var stakes = Enumerable.Range(0, opts.HorseCount).ToDictionary(index => index, _ => 0);
        foreach (var bet in bets) stakes[bet.HorseId] += bet.Amount;
        return new RaceInfo(bets.Count, HorseRules.GetCoefficients(stakes));
    }

    public async Task<TodayRaceResult> GetTodayResultAsync(long viewerBalanceScopeId, CancellationToken ct)
    {
        var raceDate = HorseTimeHelper.GetRaceDate(opts.TimezoneOffsetHours);
        var local = await resultStore.FindAsync(raceDate, viewerBalanceScopeId, ct).ConfigureAwait(false);
        if (local is not null) return new(local.Winner, local.FileId);
        var global = await resultStore.FindAsync(raceDate, 0, ct).ConfigureAwait(false);
        return global is null ? new(null, null) : new(global.Winner, global.FileId);
    }

    public Task SaveFileIdAsync(string raceDate, long balanceScopeId, string fileId, CancellationToken ct) =>
        resultStore.SaveFileIdAsync(raceDate, balanceScopeId, fileId, ct);

    public async Task<RaceOutcome> RunRaceAsync(
        long callerUserId, HorseRunKind kind, long chatScopeId, CancellationToken ct)
    {
        var raceDate = HorseTimeHelper.GetRaceDate(opts.TimezoneOffsetHours);
        var resultScope = kind == HorseRunKind.Global ? 0L : chatScopeId;
        var bets = kind == HorseRunKind.Global
            ? await betStore.ListByRaceDateAsync(raceDate, ct).ConfigureAwait(false)
            : await betStore.ListByRaceDateAndScopeAsync(raceDate, chatScopeId, ct).ConfigureAwait(false);
        var commandId = $"horse:run:{raceDate}:{kind}:{resultScope}";
        var outcome = await runExecutor.ExecuteAsync(new(new HorseRunCommand(
            callerUserId, kind, chatScopeId, resultScope, raceDate, bets, commandId,
            opts.HorseCount, opts.MinBetsToRun, opts.Admins.Contains(callerUserId))), ct)
            .ConfigureAwait(false);
        if (outcome.Error != HorseError.None) return outcome;

        var variants = Math.Max(1, opts.RenderVariants);
        var variant = SHA256.HashData(Encoding.UTF8.GetBytes(commandId))[0] % variants;
        var artifact = await renders.GetOrRenderAsync(
            new HorseRaceRenderSpec(opts.HorseCount, outcome.Winner, variant),
            RenderPriority.Interactive,
            ct).ConfigureAwait(false);
        await renderHistory.RecordAsync(new RenderHistoryEntry(
            "horse",
            resultScope.ToString(System.Globalization.CultureInfo.InvariantCulture),
            commandId,
            artifact.Key,
            timeProvider.GetUtcNow(),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["race_date"] = raceDate,
                ["winner"] = outcome.Winner.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["kind"] = kind.ToString(),
                ["variant"] = variant.ToString(System.Globalization.CultureInfo.InvariantCulture),
            }), ct).ConfigureAwait(false);
        return outcome with { GifBytes = artifact.Content };
    }

    public static Dictionary<int, double> GetKoefs(Dictionary<int, int> stakes) =>
        HorseRules.GetCoefficients(stakes);

    private static Guid StableGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
