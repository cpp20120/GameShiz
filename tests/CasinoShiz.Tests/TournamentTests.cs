using System.Globalization;
using System.Reflection;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.UpdateHandling;
using Games.Meta.Application.Meta;
using Games.Meta.Application.Tournaments;
using Games.Meta.Domain.Seasons;
using Games.Meta.Domain.Tournaments;
using Games.Meta.Infrastructure.History;
using Games.Meta.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class TournamentHandlerTests
{
    [Fact]
    public async Task TournamentHandler_RoutesCoreCommands()
    {
        var harness = CreateHarness();
        var user = new User { Id = 42, FirstName = "Alice", Username = "alice" };
        const long chatId = 100L;
        const int messageId = 10;

        await InvokeAsync(harness, "/tournament", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request => Assert.Contains("/tournament create", request.Text, StringComparison.Ordinal));

        await InvokeAsync(harness, "/tournament create", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request => Assert.Contains("Использование", request.Text, StringComparison.Ordinal));

        harness.Service.CreateResult = new TournamentCreateResult(Created: true, "created", Tournament(7, "dice", "open", 100, 8, 42, 7_500));
        await InvokeAsync(harness, "/tournament create dice 100 8", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request =>
        {
            Assert.Contains("ID <code>7</code>", request.Text, StringComparison.Ordinal);
            Assert.Contains("dice", request.Text, StringComparison.Ordinal);
        });

        await InvokeAsync(harness, "/tour join", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request => Assert.Contains("Использование", request.Text, StringComparison.Ordinal));

        harness.Service.JoinResult = new TournamentJoinResult(Joined: true, "joined", Tournament(7, "dice", "open", 100, 8, 42, 7_500, 2));
        await InvokeAsync(harness, "/tour join 7", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request =>
        {
            Assert.Contains("Турнир <code>7</code>", request.Text, StringComparison.Ordinal);
            Assert.Contains("2/8", request.Text, StringComparison.Ordinal);
        });

        harness.Service.GetResult = Tournament(7, "dice", "open", 100, 8, 42, 7_500, 2);
        await InvokeAsync(harness, "/tour status 7", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request => Assert.Contains("Турнир #7", request.Text, StringComparison.Ordinal));

        harness.Service.Players = [new TournamentPlayerInfo(7, 42, "Alice", "joined", DateTimeOffset.UtcNow)];
        await InvokeAsync(harness, "/tour players 7", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request => Assert.Contains("Alice", request.Text, StringComparison.Ordinal));

        harness.Service.Matches = [new TournamentMatchInfo(1, 7, 1, 1, "ready", 42, "Alice", 77, "Bob", VictorUserId: null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)];
        await InvokeAsync(harness, "/tour bracket 7", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request =>
        {
            Assert.Contains("Сетка турнира #7", request.Text, StringComparison.Ordinal);
            Assert.Contains("Alice", request.Text, StringComparison.Ordinal);
            Assert.Contains("Bob", request.Text, StringComparison.Ordinal);
        });

        harness.Service.ReportResult = new TournamentReportResult(Updated: true, Finished: true, "done", harness.Service.Matches[0], new TournamentPlayerInfo(7, 42, "Alice", "winner", DateTimeOffset.UtcNow));
        await InvokeAsync(harness, "/tour report 1 42", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request => Assert.Contains("Победитель", request.Text, StringComparison.Ordinal));

        harness.Service.Open = [Tournament(7, "dice", "open", 100, 8, 42, 7_500, 2)];
        await InvokeAsync(harness, "/tour list", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request => Assert.Contains("Открытые турниры", request.Text, StringComparison.Ordinal));

        harness.Service.StartResult = true;
        await InvokeAsync(harness, "/tour start 7", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request => Assert.Contains("стартовал", request.Text, StringComparison.Ordinal));

        harness.Service.FinishResult = new TournamentPlayerInfo(7, 42, "Alice", "winner", DateTimeOffset.UtcNow);
        await InvokeAsync(harness, "/tour finish 7 42", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request => Assert.Contains("завершён", request.Text, StringComparison.Ordinal));

        harness.Service.CancelResult = [new TournamentPlayerInfo(7, 42, "Alice", "refunded", DateTimeOffset.UtcNow)];
        await InvokeAsync(harness, "/tour cancel 7", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request => Assert.Contains("отменён", request.Text, StringComparison.Ordinal));
    }

    private static Harness CreateHarness()
    {
        var service = new TournamentServiceStub
        {
            CreateResult = new TournamentCreateResult(Created: false, "created"),
            JoinResult = new TournamentJoinResult(Joined: false, "joined"),
            GetResult = null,
            Open = [],
            Players = [],
            Matches = [],
            StartResult = false,
            ReportResult = new TournamentReportResult(Updated: false, Finished: false, "report"),
            FinishResult = null,
            CancelResult = null,
        };
        return new Harness(
            new RecordingBotClient(),
            new TournamentHandler(service),
            service,
            new MetaSeason(7, "Season 7", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(14), "active", "{}"));
    }

    private static async Task InvokeAsync(Harness harness, string text, User user, long chatId, int messageId)
    {
        harness.Bot.Clear();
        var update = new Update
        {
            Id = 1,
            Message = new Message
            {
                Id = messageId,
                Text = text,
                From = user,
                Chat = new Chat { Id = chatId, Type = ChatType.Private },
                Date = DateTime.UtcNow,
            },
        };

        await harness.Handler.HandleAsync(new UpdateContext(harness.BotClient, update, null!, default));
    }

    private static TournamentInfo Tournament(long id, string gameKey, string status, int entryFee, int maxPlayers, long createdBy, long prizePool, int playerCount = 0) =>
        new(id, 7, 100, gameKey, "single_elim", status, entryFee, maxPlayers, createdBy, DateTimeOffset.UtcNow, playerCount, prizePool);

    private static void AssertRequestCall<TRequest>(RecordingBotClient bot, Action<TRequest> assert)
        where TRequest : class
    {
        var call = bot.Calls.LastOrDefault(x => string.Equals(x.MethodName, "SendRequest", StringComparison.Ordinal) && x.Args["request"] is TRequest)
            ?? throw new Xunit.Sdk.XunitException(
                $"Expected request {typeof(TRequest).Name}. Actual calls: {string.Join(", ", bot.Calls.Select(x => $"{x.MethodName}<{x.Args.GetValueOrDefault("request")?.GetType().Name}>"))}");

        assert((TRequest)call.Args["request"]!);
    }

    private sealed record Harness(
        RecordingBotClient Bot,
        TournamentHandler Handler,
        TournamentServiceStub Service,
        MetaSeason Season)
    {
        public ITelegramBotClient BotClient => Bot.Create();
    }

    private sealed class TournamentServiceStub : ITournamentService
    {
        public TournamentCreateResult? CreateResult { get; set; }
        public TournamentJoinResult? JoinResult { get; set; }
        public TournamentInfo? GetResult { get; set; }
        public IReadOnlyList<TournamentInfo> Open { get; set; } = [];
        public IReadOnlyList<TournamentPlayerInfo> Players { get; set; } = [];
        public IReadOnlyList<TournamentMatchInfo> Matches { get; set; } = [];
        public bool StartResult { get; set; }
        public TournamentReportResult ReportResult { get; set; } = new(Updated: false, Finished: false, "report");
        public TournamentPlayerInfo? FinishResult { get; set; }
        public IReadOnlyList<TournamentPlayerInfo>? CancelResult { get; set; }

        public int CreateCalls { get; private set; }
        public int JoinCalls { get; private set; }
        public int StartCalls { get; private set; }
        public int ReportCalls { get; private set; }
        public int FinishCalls { get; private set; }
        public int CancelCalls { get; private set; }

        public Task<TournamentCreateResult> CreateAsync(long chatId, long userId, string gameKey, int entryFee, int maxPlayers, CancellationToken ct)
        {
            CreateCalls++;
            return Task.FromResult(CreateResult);
        }

        public Task<TournamentJoinResult> JoinAsync(long tournamentId, long userId, long chatId, string displayName, CancellationToken ct)
        {
            JoinCalls++;
            return Task.FromResult(JoinResult);
        }

        public Task<TournamentInfo?> GetAsync(long tournamentId, CancellationToken ct) => Task.FromResult(GetResult);
        public Task<IReadOnlyList<TournamentInfo>> GetOpenAsync(long chatId, int limit, CancellationToken ct) => Task.FromResult(Open);
        public Task<IReadOnlyList<TournamentPlayerInfo>> GetPlayersAsync(long tournamentId, CancellationToken ct) => Task.FromResult(Players);
        public Task<IReadOnlyList<TournamentMatchInfo>> GetMatchesAsync(long tournamentId, CancellationToken ct) => Task.FromResult(Matches);

        public Task<bool> StartAsync(long tournamentId, long userId, CancellationToken ct)
        {
            StartCalls++;
            return Task.FromResult(StartResult);
        }

        public Task<TournamentReportResult> ReportMatchAsync(long matchId, long actorUserId, long victorUserId, CancellationToken ct)
        {
            ReportCalls++;
            return Task.FromResult(ReportResult);
        }

        public Task<TournamentPlayerInfo?> FinishAsync(long tournamentId, long actorUserId, long victorUserId, CancellationToken ct)
        {
            FinishCalls++;
            return Task.FromResult(FinishResult);
        }

        public Task<IReadOnlyList<TournamentPlayerInfo>?> CancelAsync(long tournamentId, long actorUserId, CancellationToken ct)
        {
            CancelCalls++;
            return Task.FromResult(CancelResult);
        }
    }

    private class RecordingBotClient : DispatchProxy
    {
        private static readonly AsyncLocal<List<RecordedCall>?> CurrentCalls = new();
        public List<RecordedCall> Calls { get; } = [];

        public ITelegramBotClient Create()
        {
            CurrentCalls.Value = Calls;
            return DispatchProxy.Create<ITelegramBotClient, RecordingBotClient>();
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
                throw new InvalidOperationException("Missing target method.");

            var parameters = targetMethod.GetParameters();
            var map = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < parameters.Length; i++)
                map[parameters[i].Name ?? string.Create(CultureInfo.InvariantCulture, $"arg{i}")] = args?[i];

            CurrentCalls.Value?.Add(new RecordedCall(targetMethod.Name, map));
            return CreateReturnValue(targetMethod.ReturnType);
        }

        public void Clear() => Calls.Clear();

        private static object? CreateReturnValue(Type returnType)
        {
            if (returnType == typeof(void))
                return null;
            if (returnType == typeof(Task))
                return Task.CompletedTask;
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var type = returnType.GetGenericArguments()[0];
                var method = typeof(RecordingBotClient).GetMethod(nameof(CreateTaskResult), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(type);
                return method.Invoke(null, parameters: null);
            }
            if (returnType.IsValueType)
                return Activator.CreateInstance(returnType);
            return null;
        }

        private static Task<T> CreateTaskResult<T>() => Task.FromResult(default(T)!);
    }

    private sealed record RecordedCall(string MethodName, IReadOnlyDictionary<string, object?> Args);
}

public sealed class TournamentServiceTests
{
    [Fact]
    public async Task TournamentService_CoversCoreBranches()
    {
        var meta = new MetaServiceStub();
        var store = new TournamentStoreStub();
        var economics = new FakeEconomicsService();
        var history = new RecordingHistoryStore();
        var service = new TournamentService(meta, store, economics, history);
        var season = new MetaSeason(7, "Season 7", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(14), "active", "{}");
        meta.Season = season;

        store.CreateResult = new TournamentCreateResult(Created: true, "created", Tournament(7, 7, 100, "dice", "open", 100, 8, 42, 7_500));
        var created = await service.CreateAsync(100, 42, "dice", 100, 8, default);
        Assert.True(created.Created);
        Assert.Contains(history.Appends, x => string.Equals(x.EventType, "tournament.created", StringComparison.Ordinal));

        store.GetResult = Tournament(7, 7, 100, "dice", "open", 100, 8, 42, 7_500);
        store.JoinResult = new TournamentJoinResult(Joined: true, "joined", Tournament(7, 7, 100, "dice", "open", 100, 8, 42, 7_500, 1));
        var joined = await service.JoinAsync(7, 42, 100, "Alice", default);
        Assert.True(joined.Joined);
        Assert.Single(economics.Debits);
        Assert.Contains(history.Appends, x => string.Equals(x.EventType, "tournament.joined", StringComparison.Ordinal));

        store.JoinResult = new TournamentJoinResult(Joined: false, "rejected", Tournament(7, 7, 100, "dice", "open", 100, 8, 42, 7_500, 1));
        economics.Credits.Clear();
        var rejected = await service.JoinAsync(7, 43, 100, "Bob", default);
        Assert.False(rejected.Joined);
        Assert.Contains(economics.Credits, x => string.Equals(x.Reason, "tournament.entry_fee.refund", StringComparison.Ordinal));

        store.GetResult = Tournament(7, 7, 100, "dice", "open", 100, 8, 42, 7_500, 2);
        store.Matches = [new TournamentMatchInfo(1, 7, 1, 1, "ready", 42, "Alice", 77, "Bob", VictorUserId: null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)];
        store.StartResult = true;
        var started = await service.StartAsync(7, 42, default);
        Assert.True(started);
        Assert.Contains(history.Appends, x => string.Equals(x.EventType, "tournament.started", StringComparison.Ordinal));

        store.ReportResult = new TournamentReportResult(Updated: true, Finished: true, "reported", store.Matches[0], new TournamentPlayerInfo(7, 42, "Alice", "winner", DateTimeOffset.UtcNow));
        var report = await service.ReportMatchAsync(1, 42, 42, default);
        Assert.True(report.Updated);
        Assert.True(report.Finished);
        Assert.Contains(economics.Credits, x => string.Equals(x.Reason, "tournament.prize", StringComparison.Ordinal));
        Assert.Contains(history.Appends, x => string.Equals(x.EventType, "tournament.finished", StringComparison.Ordinal));

        store.FinishResult = new TournamentPlayerInfo(7, 42, "Alice", "winner", DateTimeOffset.UtcNow);
        var winner = await service.FinishAsync(7, 42, 42, default);
        Assert.NotNull(winner);

        store.CancelResult = [new TournamentPlayerInfo(7, 42, "Alice", "refunded", DateTimeOffset.UtcNow)];
        var cancelled = await service.CancelAsync(7, 42, default);
        Assert.NotNull(cancelled);
        Assert.Contains(history.Appends, x => string.Equals(x.EventType, "tournament.cancelled", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TournamentService_CoversRemainingEarlyReturns()
    {
        var meta = new MetaServiceStub();
        var store = new TournamentStoreStub();
        var economics = new FakeEconomicsService();
        var history = new RecordingHistoryStore();
        var service = new TournamentService(meta, store, economics, history);
        var season = new MetaSeason(7, "Season 7", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(14), "active", "{}");
        meta.Season = season;

        store.CreateResult = new TournamentCreateResult(Created: false, "nope");
        var create = await service.CreateAsync(100, 42, "dice", 100, 8, default);
        Assert.False(create.Created);
        Assert.DoesNotContain(history.Appends, x => string.Equals(x.EventType, "tournament.created", StringComparison.Ordinal));

        store.GetResult = null;
        var missing = await service.JoinAsync(7, 42, 100, "Alice", default);
        Assert.False(missing.Joined);
        Assert.Equal("Турнир не найден.", missing.Message);

        store.GetResult = Tournament(7, 7, 200, "dice", "open", 100, 8, 42, 0);
        var wrongChat = await service.JoinAsync(7, 42, 100, "Alice", default);
        Assert.False(wrongChat.Joined);
        Assert.Equal("Этот турнир создан в другом чате.", wrongChat.Message);

        store.GetResult = Tournament(7, 7, 100, "dice", "open", 0, 8, 42, 0);
        store.JoinResult = new TournamentJoinResult(Joined: true, "joined", Tournament(7, 7, 100, "dice", "open", 0, 8, 42, 0, 1));
        economics.Debits.Clear();
        var joinedNoFee = await service.JoinAsync(7, 42, 100, "Alice", default);
        Assert.True(joinedNoFee.Joined);
        Assert.Empty(economics.Debits);

        store.StartResult = false;
        var notStarted = await service.StartAsync(7, 42, default);
        Assert.False(notStarted);
        Assert.DoesNotContain(history.Appends, x => string.Equals(x.EventType, "tournament.started", StringComparison.Ordinal));

        store.ReportResult = new TournamentReportResult(Updated: false, Finished: false, "rejected");
        var notReported = await service.ReportMatchAsync(1, 42, 42, default);
        Assert.False(notReported.Updated);

        store.ReportResult = new TournamentReportResult(Updated: true, Finished: false, "updated", new TournamentMatchInfo(1, 7, 1, 1, "ready", 42, "Alice", 77, "Bob", VictorUserId: null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow), Victor: null);
        var unfinished = await service.ReportMatchAsync(1, 42, 42, default);
        Assert.True(unfinished.Updated);
        Assert.False(unfinished.Finished);

        store.GetResult = null;
        var noFinish = await service.FinishAsync(7, 42, 42, default);
        Assert.Null(noFinish);

        store.GetResult = Tournament(7, 7, 100, "dice", "open", 100, 8, 42, 0);
        store.FinishResult = null;
        var finishNull = await service.FinishAsync(7, 42, 42, default);
        Assert.Null(finishNull);

        store.CancelResult = null;
        var cancelNull = await service.CancelAsync(7, 42, default);
        Assert.Null(cancelNull);

        store.CancelResult = [];
        store.GetResult = null;
        var cancelMissing = await service.CancelAsync(7, 42, default);
        Assert.Null(cancelMissing);

        var open = await service.GetOpenAsync(100, 10, default);
        Assert.Empty(open);
        var players = await service.GetPlayersAsync(7, default);
        Assert.Empty(players);
        var matches = await service.GetMatchesAsync(7, default);
        Assert.Empty(matches);
        var current = await service.GetAsync(7, default);
        Assert.Null(current);
    }

    private static TournamentInfo Tournament(long id, long seasonId, long chatId, string gameKey, string status, int entryFee, int maxPlayers, long createdBy, long prizePool, int playerCount = 0) =>
        new(id, seasonId, chatId, gameKey, "single_elim", status, entryFee, maxPlayers, createdBy, DateTimeOffset.UtcNow, playerCount, prizePool);

    private sealed class MetaServiceStub : IMetaService
    {
        public MetaSeason Season { get; set; } = null!;

        public Task<MetaSeason> GetActiveSeasonAsync(CancellationToken ct) => Task.FromResult(Season);
        public Task<SeasonProfile> GetProfileAsync(long chatId, long userId, string displayName, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<SeasonLeaderboardEntry>> GetTopAsync(long chatId, int limit, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<PlayerAchievementView>> GetAchievementsAsync(long chatId, long userId, CancellationToken ct) => throw new NotImplementedException();
        public Task<GameStreakRecordResult?> RecordGamePlayedAsync(long seasonId, long chatId, long userId, string gameKey, DateOnly playedOn, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<PlayerGameStreakView>> GetGameStreaksAsync(long chatId, long userId, CancellationToken ct) => throw new NotImplementedException();
        public Task<SeasonPlayer> ApplyGameCompletedAsync(long chatId, long userId, string displayName, long stake, long payout, bool isWin, CancellationToken ct) => throw new NotImplementedException();
        public Task<SeasonPlayer> AddSeasonXpAsync(long seasonId, long chatId, long userId, string displayName, long xpDelta, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<AchievementUnlock>> UnlockAchievementsAsync(long seasonId, long chatId, long userId, IEnumerable<AchievementDefinition> achievements, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class TournamentStoreStub : ITournamentStore
    {
        public TournamentCreateResult CreateResult { get; set; } = new(Created: false, "create");
        public TournamentJoinResult JoinResult { get; set; } = new(Joined: false, "join");
        public TournamentInfo? GetResult { get; set; }
        public IReadOnlyList<TournamentInfo> Open { get; set; } = [];
        public IReadOnlyList<TournamentPlayerInfo> Players { get; set; } = [];
        public IReadOnlyList<TournamentMatchInfo> Matches { get; set; } = [];
        public bool StartResult { get; set; }
        public TournamentReportResult ReportResult { get; set; } = new(Updated: false, Finished: false, "report");
        public TournamentPlayerInfo? FinishResult { get; set; }
        public IReadOnlyList<TournamentPlayerInfo>? CancelResult { get; set; }

        public Task<TournamentCreateResult> CreateAsync(MetaSeason season, long chatId, long createdBy, string gameKey, int entryFee, int maxPlayers, CancellationToken ct) => Task.FromResult(CreateResult);
        public Task<TournamentJoinResult> JoinAsync(long tournamentId, long userId, string displayName, CancellationToken ct) => Task.FromResult(JoinResult);
        public Task<TournamentInfo?> GetAsync(long tournamentId, CancellationToken ct) => Task.FromResult(GetResult);
        public Task<IReadOnlyList<TournamentInfo>> GetOpenAsync(MetaSeason season, long chatId, int limit, CancellationToken ct) => Task.FromResult(Open);
        public Task<IReadOnlyList<TournamentPlayerInfo>> GetPlayersAsync(long tournamentId, CancellationToken ct) => Task.FromResult(Players);
        public Task<IReadOnlyList<TournamentMatchInfo>> GetMatchesAsync(long tournamentId, CancellationToken ct) => Task.FromResult(Matches);
        public Task<bool> StartAsync(long tournamentId, long userId, CancellationToken ct) => Task.FromResult(StartResult);
        public Task<TournamentReportResult> ReportMatchAsync(long matchId, long actorUserId, long victorUserId, CancellationToken ct) => Task.FromResult(ReportResult);
        public Task<TournamentPlayerInfo?> FinishAsync(long tournamentId, long actorUserId, long winnerUserId, CancellationToken ct) => Task.FromResult(FinishResult);
        public Task<IReadOnlyList<TournamentPlayerInfo>?> CancelAsync(long tournamentId, long actorUserId, CancellationToken ct) => Task.FromResult(CancelResult);
    }

    private sealed class RecordingHistoryStore : IMetaHistoryStore
    {
        public List<(string EventType, string AggregateType, string AggregateId, long? SeasonId, long? ChatId, long? UserId, object Payload)> Appends { get; } = [];
        public Task AppendAsync(string eventType, string aggregateType, string aggregateId, long? seasonId, long? chatId, long? userId, object payload, CancellationToken ct)
        {
            Appends.Add((eventType, aggregateType, aggregateId, seasonId, chatId, userId, payload));
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<MetaHistoryEvent>> ListAsync(string? eventType, string? aggregateType, string? aggregateId, long? chatId, long? userId, int limit, CancellationToken ct) => throw new NotImplementedException();
        public Task<MetaHistoryStats> GetStatsAsync(CancellationToken ct) => throw new NotImplementedException();
    }
}
