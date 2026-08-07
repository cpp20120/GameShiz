using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BotFramework.Contracts.Caching;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Games.Horse.Application.Execution;
using BotFramework.Rendering;
using Games.Horse.Rendering;
using Microsoft.Extensions.Caching.Memory;

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
    IRuntimeTuningAccessor tuning,
    IMemoryCache? localCache = null,
    ICacheStore? distributedCache = null,
    ICacheStoreInvalidator? distributedCacheInvalidator = null) : IHorseService
{
    private static readonly JsonSerializerOptions CacheJson = new(JsonSerializerDefaults.Web);

    private HorseOptions Options => tuning.GetSection<HorseOptions>(HorseOptions.SectionName);

    public int HorseCount => Options.HorseCount;
    public int MinBetsToRun => Options.MinBetsToRun;

    public Task<BetResult> PlaceBetAsync(
        long userId, string displayName, long balanceScopeId, int horseId, int amount,
        CancellationToken ct) =>
        PlaceBetAsync(userId, displayName, balanceScopeId, horseId, amount, 0, ct);

    public Task<BetResult> PlaceBetAsync(
        long userId, string displayName, long balanceScopeId, int horseId, int amount,
        int sourceMessageId, CancellationToken ct)
    {
        var opts = Options;
        var raceDate = HorseTimeHelper.GetRaceDate(opts.TimezoneOffsetHours);
        var commandId = sourceMessageId != 0
            ? $"horse:bet:{balanceScopeId}:{sourceMessageId}:{userId}"
            : $"horse:bet:legacy:{Guid.NewGuid():N}";
        return PlaceBetAndInvalidateAsync(new HorsePlaceBetCommand(
            userId, displayName, balanceScopeId, horseId, amount, raceDate,
            StableGuid(commandId), commandId, opts.HorseCount), raceDate, balanceScopeId, opts.HorseCount, ct);
    }

    public async Task<RaceInfo> GetTodayInfoAsync(long? balanceScopeIdOnly, CancellationToken ct)
    {
        var opts = Options;
        var raceDate = HorseTimeHelper.GetRaceDate(opts.TimezoneOffsetHours);
        var cacheKey = InfoCacheKey(raceDate, balanceScopeIdOnly, opts.HorseCount);
        if (localCache?.TryGetValue(cacheKey, out var localValue) == true && localValue is RaceInfo local)
            return local;

        if (distributedCache is not null)
        {
            var distributed = await TryReadDistributedAsync(cacheKey, ct);
            if (distributed is not null)
            {
                SetLocal(cacheKey, distributed, opts.InfoCacheSeconds);
                return distributed;
            }
        }

        var bets = balanceScopeIdOnly is { } scope
            ? await betStore.ListByRaceDateAndScopeAsync(raceDate, scope, ct)
            : await betStore.ListByRaceDateAsync(raceDate, ct);
        var stakes = Enumerable.Range(0, opts.HorseCount).ToDictionary(index => index, _ => 0);
        foreach (var bet in bets) stakes[bet.HorseId] += bet.Amount;
        var result = new RaceInfo(bets.Count, HorseRules.GetCoefficients(stakes));
        SetLocal(cacheKey, result, opts.InfoCacheSeconds);
        await TryWriteDistributedAsync(cacheKey, result, opts.InfoCacheSeconds);
        return result;
    }

    public async Task<TodayRaceResult> GetTodayResultAsync(long viewerBalanceScopeId, CancellationToken ct)
    {
        var opts = Options;
        var raceDate = HorseTimeHelper.GetRaceDate(opts.TimezoneOffsetHours);
        var local = await resultStore.FindAsync(raceDate, viewerBalanceScopeId, ct);
        if (local is not null) return new(local.Winner, local.FileId);
        var global = await resultStore.FindAsync(raceDate, 0, ct);
        return global is null ? new(null, null) : new(global.Winner, global.FileId);
    }

    public Task SaveFileIdAsync(string raceDate, long balanceScopeId, string fileId, CancellationToken ct) =>
        resultStore.SaveFileIdAsync(raceDate, balanceScopeId, fileId, ct);

    public async Task<RaceOutcome> RunRaceAsync(
        long callerUserId, HorseRunKind kind, long chatScopeId, CancellationToken ct)
    {
        var opts = Options;
        var raceDate = HorseTimeHelper.GetRaceDate(opts.TimezoneOffsetHours);
        var resultScope = kind == HorseRunKind.Global ? 0L : chatScopeId;
        var bets = kind == HorseRunKind.Global
            ? await betStore.ListByRaceDateAsync(raceDate, ct)
            : await betStore.ListByRaceDateAndScopeAsync(raceDate, chatScopeId, ct);
        var commandId = $"horse:run:{raceDate}:{kind}:{resultScope}";
        var outcome = await runExecutor.ExecuteAsync(new(new HorseRunCommand(
            callerUserId, kind, chatScopeId, resultScope, raceDate, bets, commandId,
            opts.HorseCount, opts.MinBetsToRun, opts.Admins.Contains(callerUserId))), ct);
        if (outcome.Error != HorseError.None) return outcome;

        var affectedScopes = outcome.BetScopeIds
            .Append(0L)
            .Append(kind == HorseRunKind.Global ? 0L : chatScopeId)
            .Distinct()
            .Select(scope => scope == 0 ? (long?)null : scope);
        await InvalidateInfoAsync(raceDate, affectedScopes, opts.HorseCount);

        var variants = Math.Max(1, opts.RenderVariants);
        var variant = SHA256.HashData(Encoding.UTF8.GetBytes(commandId))[0] % variants;
        var artifact = await renders.GetOrRenderAsync(
            new HorseRaceRenderSpec(opts.HorseCount, outcome.Winner, variant),
            RenderPriority.Interactive,
            ct);
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
            }), ct);
        return outcome with { GifBytes = artifact.Content };
    }

    public static IReadOnlyDictionary<int, double> GetKoefs(IReadOnlyDictionary<int, int> stakes) =>
        HorseRules.GetCoefficients(stakes);

    private async Task<BetResult> PlaceBetAndInvalidateAsync(
        HorsePlaceBetCommand command,
        string raceDate,
        long balanceScopeId,
        int horseCount,
        CancellationToken ct)
    {
        var result = await betExecutor.ExecuteAsync(new(command), ct);
        if (result.Error == HorseError.None)
            await InvalidateInfoAsync(raceDate, [balanceScopeId, null], horseCount);

        return result;
    }

    private async Task<RaceInfo?> TryReadDistributedAsync(string key, CancellationToken ct)
    {
        var payload = await distributedCache!.GetStringAsync(key, ct);
        if (string.IsNullOrWhiteSpace(payload)) return null;

        try
        {
            var cached = JsonSerializer.Deserialize<CachedRaceInfo>(payload, CacheJson);
            return cached is null ? null : new RaceInfo(cached.BetsCount, cached.Koefs);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void SetLocal(string key, RaceInfo value, int ttlSeconds) =>
        localCache?.Set(key, value, TimeSpan.FromSeconds(Math.Clamp(ttlSeconds, 1, 300)));

    private async Task TryWriteDistributedAsync(string key, RaceInfo value, int ttlSeconds)
    {
        if (distributedCache is null) return;

        try
        {
            var payload = JsonSerializer.Serialize(
                new CachedRaceInfo(value.BetsCount, new Dictionary<int, double>(value.Koefs)),
                CacheJson);
            await distributedCache.SetStringAsync(
                key,
                payload,
                TimeSpan.FromSeconds(Math.Clamp(ttlSeconds, 1, 300)),
                CancellationToken.None);
        }
        catch (Exception)
        {
            // The read model is best effort; PostgreSQL remains authoritative.
        }
    }

    private async Task InvalidateInfoAsync(string raceDate, IEnumerable<long?> scopes, int horseCount)
    {
        foreach (var scope in scopes.Distinct())
        {
            var key = InfoCacheKey(raceDate, scope, horseCount);
            localCache?.Remove(key);
            if (distributedCacheInvalidator is null) continue;

            try
            {
                await distributedCacheInvalidator.RemoveStringAsync(key, CancellationToken.None);
            }
            catch (Exception)
            {
                // A cache outage must not turn a committed mutation into an error.
            }
        }
    }

    private static string InfoCacheKey(string raceDate, long? balanceScopeId, int horseCount) =>
        balanceScopeId is { } scope
            ? $"horse:race-info:v1:{raceDate}:horses:{horseCount}:scope:{scope.ToString(CultureInfo.InvariantCulture)}"
            : $"horse:race-info:v1:{raceDate}:horses:{horseCount}:global";

    private sealed record CachedRaceInfo(int BetsCount, Dictionary<int, double> Koefs);

    private static Guid StableGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
