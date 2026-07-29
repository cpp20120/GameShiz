using BotFramework.Host.Admin.Execution;
using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Admin.Execution;
using BotFramework.Sdk.Execution;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Games.Admin.Application.Effects;
using Games.Admin.Infrastructure.Models;
using Games.Meta.Application.Effects;
using Games.Meta.Domain.Quests;
using Games.Meta.Domain.Seasons;
using Games.Meta.Infrastructure.Catalog;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class MetaAdminPropertyTests
{
    [Property(MaxTest = 100)]
    public async Task<Property> TournamentGameKey_NormalizationAndSupportAreStable(PositiveInt raw)
    {
        var values = new[]
        {
            "dice", "/dice", "DICE", " cube ", "/CUBE", "darts", "football", "basketball", "bowling",
            "poker", "", " / unknown ", "dicecube",
        };
        var input = values[raw.Get % values.Length];
        var normalized = TournamentHandlerProbe.Normalize(input);
        var supported = TournamentHandlerProbe.IsSupported(normalized);
        var expected = input.Trim().TrimStart('/').ToLowerInvariant() switch
        {
            "cube" => "dicecube",
            var key => key,
        };
        var expectedSupported = expected is "dice" or "dicecube" or "darts" or "football" or "basketball" or "bowling";

        return (normalized == expected && supported == expectedSupported)
            .ToProperty()
            .Label($"input='{input}', normalized='{normalized}', supported={supported}");
    }

    [Property(MaxTest = 100)]
    public async Task<Property> TournamentCreate_InvalidInputIsRejectedWithoutDatabaseMutation(
        PositiveInt rawId,
        NonNegativeInt rawGame,
        NonNegativeInt rawFee,
        NonNegativeInt rawPlayers)
    {
        var invalidKind = rawGame.Get % 3;
        var gameKey = invalidKind == 0 ? "poker" : "dice";
        var entryFee = invalidKind == 1 ? -1 : rawFee.Get % 10_000;
        var maxPlayers = invalidKind == 2
            ? 65
            : 2;
        var context = new RecordingAtomicContext();
        var handler = new TournamentCreateAtomicEffectHandler();

        await handler.ApplyAsync(
            new TournamentCreateAtomicEffect(1, rawId.Get, 10, gameKey, entryFee, maxPlayers),
            context,
            CancellationToken.None);

        var result = Assert.IsType<TournamentCreateResult>(context.Outputs["result"]);
        var expectedInvalid = !TournamentHandlerProbe.IsSupported(
            gameKey.Trim().TrimStart('/').ToLowerInvariant() switch { "cube" => "dicecube", var key => key })
            || entryFee < 0
            || maxPlayers is < 2 or > 64;

        return (expectedInvalid && !result.Created && context.QueryCalls == 0 && context.ExecuteCalls == 0)
            .ToProperty()
            .Label($"game={gameKey}, fee={entryFee}, players={maxPlayers}, queries={context.QueryCalls}");
    }

    [Property(MaxTest = 100)]
    public async Task<Property> MetaAlertStatus_LogsHistoryOnlyWhenAnOpenFlagChanged(
        PositiveInt rawFlag,
        NonNegativeInt rawStatus,
        NonNegativeInt rawChanged)
    {
        var changed = rawChanged.Get % 2;
        var status = new[] { "resolved", "dismissed", "open" }[rawStatus.Get % 3];
        var context = new RecordingAdminContext { ExecuteResult = changed };

        await ((IAdminEffectHandler)new MetaAlertStatusAdminEffectHandler()).ApplyAsync(
            new MetaAlertStatusAdminEffect(rawFlag.Get, status),
            context,
            CancellationToken.None);

        var historyCalls = context.Sql.Count(sql => sql.Contains("INSERT INTO meta_event_log", StringComparison.Ordinal));
        var valid = Equals(context.Outputs["changed"], changed)
            && historyCalls == (changed > 0 ? 1 : 0)
            && context.ExecuteCalls == 1 + historyCalls;

        return valid
            .ToProperty()
            .Label($"flag={rawFlag.Get}, changed={changed}, history={historyCalls}");
    }

    [Property(MaxTest = 100)]
    public async Task<Property> AdminDisplayNameEffect_SelectsExactlyOneMutation(
        PositiveInt raw,
        NonEmptyString original,
        NonEmptyString replacement)
    {
        var clear = raw.Get % 2 == 0;
        var context = new RecordingAdminContext();
        var effect = new DisplayNameOverrideAdminEffect(
            original.Get,
            clear ? null : replacement.Get);

        await ((IAdminEffectHandler)new DisplayNameOverrideAdminEffectHandler()).ApplyAsync(
            effect,
            context,
            CancellationToken.None);

        var sql = Assert.Single(context.Sql);
        var valid = clear
            ? sql.Contains("DELETE FROM display_name_overrides", StringComparison.Ordinal)
            : sql.Contains("INSERT INTO display_name_overrides", StringComparison.Ordinal);

        return (valid && context.ExecuteCalls == 1)
            .ToProperty()
            .Label($"clear={clear}, sql='{sql[..Math.Min(sql.Length, 32)]}'");
    }

    [Property(MaxTest = 100)]
    public async Task<Property> ClearChatBets_RefundsEveryDeletedBetExactlyOnce(NonNegativeInt raw)
    {
        var count = raw.Get % 8;
        var bets = Enumerable.Range(0, count)
            .Select(index => new PendingChatBet
            {
                GameId = new[] { "dicecube", "football", "basketball", "bowling", "darts" }[index % 5],
                UserId = index + 1,
                ChatId = 700 + index,
                Amount = index + 2,
                BotMessageId = index % 2 == 0 ? index + 10 : null,
            })
            .ToArray();
        var wallet = new RecordingWallet();
        var context = new RecordingAdminContext { Wallet = wallet, QueryResults = [bets] };

        await ((IAdminEffectHandler)new ClearChatBetsAdminEffectHandler()).ApplyAsync(
            new ClearChatBetsAdminEffect(42),
            context,
            CancellationToken.None);

        var output = Assert.IsAssignableFrom<IReadOnlyList<PendingChatBet>>(context.Outputs["bets"]);
        var valid = output.SequenceEqual(bets)
            && wallet.Mutations.Count == count
            && wallet.Mutations.Select(x => x.Effect.Amount).SequenceEqual(bets.Select(x => x.Amount))
            && wallet.Mutations.All(x => x.Effect.Kind == WalletBatchEffectKind.Credit)
            && context.QueryCalls == 5;

        return valid
            .ToProperty()
            .Label($"bets={count}, refunds={wallet.Mutations.Count}, queries={context.QueryCalls}");
    }

    [Property(MaxTest = 100)]
    public async Task<Property> QuestProgress_MissingSeasonProducesNoUpdates(PositiveInt raw)
    {
        var context = new RecordingAtomicContext();
        var handler = new QuestProgressAtomicEffectHandler(new EmptyQuestCatalog());
        await handler.ApplyAsync(
            new QuestProgressAtomicEffect(raw.Get, 10, 20, new BotFramework.Sdk.Events.Meta.GameCompletedMetaEvent(
                10, 20, "user", "dice", 1, 1, true, 1, 0), DateTimeOffset.UnixEpoch),
            context,
            CancellationToken.None);

        var updates = Assert.IsAssignableFrom<IReadOnlyList<QuestProgressUpdate>>(context.Outputs["updates"]);
        return (updates.Count == 0 && context.QueryCalls == 1)
            .ToProperty()
            .Label($"season={raw.Get}, queries={context.QueryCalls}");
    }

    private sealed class TournamentHandlerProbe : TournamentAtomicEffectHandler<TournamentCreateAtomicEffect>
    {
        protected override Task ApplyAsync(TournamentCreateAtomicEffect effect, IAtomicEffectContext context, CancellationToken ct) =>
            Task.CompletedTask;

        public static string Normalize(string value) => NormalizeGameKey(value);
        public static bool IsSupported(string value) => IsSupportedGame(value);
    }

    private sealed class EmptyQuestCatalog : IQuestCatalog
    {
        public IReadOnlyList<QuestTemplate> All => [];
        public void Reload() { }
        public IReadOnlyList<QuestTemplate> ActiveFor(MetaSeason season, long chatId, long userId, DateTimeOffset now, QuestPlayerProgress? progress = null) => [];
        public IEnumerable<QuestTemplate> Matching(MetaSeason season, long chatId, long userId, BotFramework.Sdk.Events.Meta.GameCompletedMetaEvent ev, QuestPlayerProgress? progress = null) => [];
        public QuestTemplate? FindActive(MetaSeason season, long chatId, long userId, string questId, DateTimeOffset now, QuestPlayerProgress? progress = null) => null;
    }

    private sealed class RecordingAdminContext : IAdminExecutionContext
    {
        public AdminActor Actor { get; } = new(99, "property-test");
        public string Action { get; } = "property-test";
        public IWalletAtomicExecutionService? Wallet { get; init; }
        public int ExecuteResult { get; init; }
        public IReadOnlyList<PendingChatBet[]> QueryResults { get; init; } = [];
        public List<string> Sql { get; } = [];
        public Dictionary<string, object?> Outputs { get; } = new(StringComparer.Ordinal);
        public int ExecuteCalls { get; private set; }
        public int QueryCalls { get; private set; }

        public Task<int> ExecuteAsync(string sql, object? parameters, CancellationToken ct)
        {
            ExecuteCalls++;
            Sql.Add(sql);
            return Task.FromResult(ExecuteResult);
        }

        public Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters, CancellationToken ct) =>
            Task.FromResult<T?>(default);

        public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters, CancellationToken ct)
        {
            QueryCalls++;
            if (typeof(T) == typeof(PendingChatBet))
            {
                var result = QueryCalls == 1 && QueryResults.Count > 0
                    ? QueryResults[0].Cast<T>().ToArray()
                    : [];
                return Task.FromResult<IReadOnlyList<T>>(result);
            }
            return Task.FromResult<IReadOnlyList<T>>([]);
        }

        public void SetOutput(string key, object? value) => Outputs[key] = value;
    }

    private sealed class RecordingAtomicContext : IAtomicEffectContext
    {
        public Dictionary<string, object?> Outputs { get; } = new(StringComparer.Ordinal);
        public int ExecuteCalls { get; private set; }
        public int QueryCalls { get; private set; }

        public Task<int> ExecuteAsync(string sql, object? parameters, CancellationToken ct)
        {
            ExecuteCalls++;
            return Task.FromResult(0);
        }

        public Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters, CancellationToken ct)
        {
            QueryCalls++;
            return Task.FromResult<T?>(default);
        }

        public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<T>>([]);

        public void SetOutput(string key, object? value) => Outputs[key] = value;
    }

    private sealed class RecordingWallet : IWalletAtomicExecutionService
    {
        public List<WalletMutation> Mutations { get; } = [];
        public Task EnsureUserAsync(long userId, long balanceScopeId, string displayName, CancellationToken ct) => Task.CompletedTask;
        public Task<int> GetBalanceAsync(long userId, long balanceScopeId, CancellationToken ct) => Task.FromResult(0);
        public Task<WalletBatchMutationResult> ApplyBatchAsync(long userId, long balanceScopeId, IReadOnlyList<WalletBatchEffect> effects, string operationId, CancellationToken ct)
        {
            Mutations.Add(new WalletMutation(userId, balanceScopeId, effects.Single(), operationId));
            return Task.FromResult(new WalletBatchMutationResult(true, false, 0));
        }
    }

    private sealed record WalletMutation(long UserId, long ChatId, WalletBatchEffect Effect, string OperationId);
}
