using System.Reflection;
using BotFramework.Rendering;
using Microsoft.Extensions.Logging.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class PokerHandlerTests
{
    [Fact]
    public async Task CommandFromPrivateChat_IsRejected()
    {
        var service = new PokerServiceStub();
        var recorder = new BotProxy();

        await InvokeAsync(
            new PokerHandler(
                service,
                new EchoLocalizer(),
                new FakeRuntimeTuning(),
                new RenderQueueStub(),
                new RenderHistoryStub(),
                TimeProvider.System,
                NullLogger<PokerHandler>.Instance),
            recorder,
            "/poker status",
            ChatType.Private);

        var request = Assert.IsType<SendMessageRequest>(recorder.Requests.Single());
        Assert.Equal("poker:err.only_group", request.Text);
        Assert.Equal(0, service.FindCalls);
    }

    [Fact]
    public async Task JoinWithoutCode_UsesCurrentTableAndReportsError()
    {
        var service = new PokerServiceStub
        {
            JoinResult = new JoinResult(PokerError.TableNotFound, null, 0, 8),
        };
        var recorder = new BotProxy();
        var handler = CreateHandler(service);

        await InvokeAsync(handler, recorder, "/poker join", ChatType.Group);

        var request = Assert.IsType<SendMessageRequest>(recorder.Requests.Single());
        Assert.Equal("poker:err.table_not_found", request.Text);
        Assert.Equal(1, service.JoinSourceCalls);
    }

    [Fact]
    public async Task JoinSuccess_ReportsSeatCount()
    {
        var service = new PokerServiceStub
        {
            JoinResult = new JoinResult(PokerError.None, null, 2, 8),
        };
        var recorder = new BotProxy();

        await InvokeAsync(CreateHandler(service), recorder, "/poker join abc", ChatType.Group);

        var request = Assert.IsType<SendMessageRequest>(recorder.Requests.Single());
        Assert.Contains("ABC", request.Text, StringComparison.Ordinal);
        Assert.Contains("2/8", request.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallbackForAnotherPlayer_IsRejectedWithAlert()
    {
        var service = new PokerServiceStub();
        var recorder = new BotProxy();
        var update = new Update
        {
            Id = 1,
            CallbackQuery = new CallbackQuery
            {
                Id = "callback-1",
                Data = "poker:check:77",
                From = new User { Id = 42, FirstName = "Alice" },
                Message = new Message
                {
                    Id = 9,
                    Chat = new Chat { Id = 100, Type = ChatType.Group },
                    Date = DateTime.UtcNow,
                },
            },
        };

        await CreateHandler(service).HandleAsync(new UpdateContext(
            recorder.Create(), update, null!, CancellationToken.None));

        var request = Assert.IsType<AnswerCallbackQueryRequest>(recorder.Requests.Single());
        Assert.Equal("callback-1", request.CallbackQueryId);
        Assert.Equal("poker:err.action_for_other_player", request.Text);
        Assert.True(request.ShowAlert);
        Assert.Equal(0, service.ActionCalls);
    }

    [Fact]
    public async Task CardsWithoutTable_ReturnsAlert()
    {
        var service = new PokerServiceStub();
        var recorder = new BotProxy();
        var update = new Update
        {
            Id = 1,
            CallbackQuery = new CallbackQuery
            {
                Id = "callback-2",
                Data = "poker:cards",
                From = new User { Id = 42, FirstName = "Alice" },
                Message = new Message
                {
                    Id = 9,
                    Chat = new Chat { Id = 100, Type = ChatType.Group },
                    Date = DateTime.UtcNow,
                },
            },
        };

        await CreateHandler(service).HandleAsync(new UpdateContext(
            recorder.Create(), update, null!, CancellationToken.None));

        var request = Assert.IsType<AnswerCallbackQueryRequest>(recorder.Requests.Single());
        Assert.Equal("poker:err.no_table", request.Text);
        Assert.True(request.ShowAlert);
    }

    [Fact]
    public async Task InvalidCallback_IsAcknowledged()
    {
        var recorder = new BotProxy();
        var update = new Update
        {
            Id = 1,
            CallbackQuery = new CallbackQuery
            {
                Id = "callback-3",
                Data = "stale",
                From = new User { Id = 42, FirstName = "Alice" },
            },
        };

        await CreateHandler(new PokerServiceStub()).HandleAsync(new UpdateContext(
            recorder.Create(), update, null!, CancellationToken.None));

        var request = Assert.IsType<AnswerCallbackQueryRequest>(recorder.Requests.Single());
        Assert.Equal("callback-3", request.CallbackQueryId);
        Assert.Null(request.Text);
    }

    private static PokerHandler CreateHandler(PokerServiceStub service) => new(
        service,
        new EchoLocalizer(),
        new FakeRuntimeTuning(),
        new RenderQueueStub(),
        new RenderHistoryStub(),
        TimeProvider.System,
        NullLogger<PokerHandler>.Instance);

    private static async Task InvokeAsync(
        PokerHandler handler,
        BotProxy recorder,
        string text,
        ChatType chatType)
    {
        var update = new Update
        {
            Id = 1,
            Message = new Message
            {
                Id = 9,
                Text = text,
                From = new User { Id = 42, FirstName = "Alice" },
                Chat = new Chat { Id = 100, Type = chatType },
                Date = DateTime.UtcNow,
            },
        };

        await handler.HandleAsync(new UpdateContext(recorder.Create(), update, null!, CancellationToken.None));
    }

    private sealed class PokerServiceStub : IPokerService
    {
        public JoinResult JoinResult { get; init; } = new(PokerError.None, null, 1, 8);
        public int FindCalls { get; private set; }
        public int JoinSourceCalls { get; private set; }
        public int ActionCalls { get; private set; }

        public Task<(TableSnapshot? Snapshot, PokerSeat? MySeat)> FindMyTableAsync(long userId, long currentChatId, CancellationToken ct)
        {
            FindCalls++;
            return Task.FromResult<(TableSnapshot?, PokerSeat?)>((null, null));
        }

        public Task<CreateResult> CreateTableAsync(long userId, string displayName, long chatId, CancellationToken ct) =>
            Task.FromResult(new CreateResult(PokerError.None, "ABC", 100));

        public Task<CreateResult> CreateTableAsync(long userId, string displayName, long chatId, int sourceMessageId, CancellationToken ct) =>
            Task.FromResult(new CreateResult(PokerError.None, "ABC", 100));

        public Task<JoinResult> JoinTableAsync(long userId, string displayName, long chatId, string code, CancellationToken ct) =>
            Task.FromResult(JoinResult);

        public Task<JoinResult> JoinTableAsync(long userId, string displayName, long chatId, string code, int sourceMessageId, CancellationToken ct)
        {
            JoinSourceCalls++;
            return Task.FromResult(JoinResult);
        }

        public Task<StartResult> StartHandAsync(long userId, long currentChatId, CancellationToken ct) =>
            Task.FromResult(new StartResult(PokerError.NoTable, null));

        public Task<ActionResult> ApplyPlayerActionAsync(long userId, long currentChatId, string verb, int amount, CancellationToken ct)
        {
            ActionCalls++;
            return Task.FromResult(new ActionResult(PokerError.NoTable, null, default, null, null, null));
        }

        public Task<ActionResult?> RunAutoActionAsync(string inviteCode, CancellationToken ct) => Task.FromResult<ActionResult?>(null);
        public Task<LeaveResult> LeaveTableAsync(long userId, long currentChatId, CancellationToken ct) =>
            Task.FromResult(new LeaveResult(PokerError.NoTable, null, false));
        public Task SetTableStateMessageIdAsync(string inviteCode, int messageId, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> ListStuckCodesAsync(long cutoffMs, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class EchoLocalizer : ILocalizer
    {
        public string Get(string moduleId, string key, string cultureCode = "ru") =>
            key == "joined" ? "{0} {1}/{2}" : $"{moduleId}:{key}";
        public string GetPlural(string moduleId, string key, int count, string cultureCode = "ru") => Get(moduleId, key, cultureCode);
    }

    private sealed class RenderQueueStub : IRenderQueue
    {
        public ValueTask<RenderedArtifact> GetOrRenderAsync<TSpec>(TSpec spec, RenderPriority priority = RenderPriority.Interactive, CancellationToken ct = default) =>
            ValueTask.FromException<RenderedArtifact>(new NotSupportedException());

        public Task PrewarmAsync<TSpec>(IEnumerable<TSpec> specs, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class RenderHistoryStub : IRenderHistory
    {
        public ValueTask RecordAsync(RenderHistoryEntry entry, CancellationToken ct = default) => ValueTask.CompletedTask;

        public IAsyncEnumerable<RenderHistoryEntry> ListAsync(string gameId, string aggregateId, int take = 50, CancellationToken ct = default) => Empty();

        private static async IAsyncEnumerable<RenderHistoryEntry> Empty()
        {
            yield break;
        }
    }

    private class BotProxy : DispatchProxy
    {
        private static readonly AsyncLocal<List<object>?> Current = new();
        public List<object> Requests { get; } = [];

        public ITelegramBotClient Create()
        {
            Current.Value = Requests;
            return DispatchProxy.Create<ITelegramBotClient, BotProxy>();
        }

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name == "SendRequest" && args?[0] is object request)
                Current.Value?.Add(request);
            if (method is null) return null;

            var returnType = method.ReturnType;
            if (returnType == typeof(Task)) return Task.CompletedTask;
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var helper = typeof(BotProxy).GetMethod(nameof(Result), BindingFlags.Static | BindingFlags.NonPublic)!
                    .MakeGenericMethod(returnType.GetGenericArguments()[0]);
                return helper.Invoke(null, null);
            }

            return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
        }

        private static Task<T> Result<T>() => Task.FromResult(default(T)!);
    }
}
