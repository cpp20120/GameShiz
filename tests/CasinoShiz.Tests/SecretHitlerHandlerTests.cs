using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class SecretHitlerHandlerTests
{
    [Fact]
    public async Task UnsupportedChat_IsRejected()
    {
        var recorder = new BotProxy();
        await InvokeAsync(CreateHandler(new SecretHitlerServiceStub()), recorder, "/sh status", ChatType.Channel);

        var request = Assert.IsType<SendMessageRequest>(recorder.Requests.Single());
        Assert.Equal("sh:err.unsupported_chat", request.Text);
    }

    [Fact]
    public async Task JoinWithoutCode_ReturnsValidationError()
    {
        var service = new SecretHitlerServiceStub();
        var recorder = new BotProxy();
        await InvokeAsync(CreateHandler(service), recorder, "/sh join", ChatType.Private);

        var request = Assert.IsType<SendMessageRequest>(recorder.Requests.Single());
        Assert.Equal("sh:err.join_missing_code", request.Text);
        Assert.Equal(0, service.JoinCalls);
    }

    [Fact]
    public async Task CreateError_IsReportedWithConfiguredBuyIn()
    {
        var service = new SecretHitlerServiceStub
        {
            CreateResult = new ShCreateResult(ShError.NotEnoughCoins, "ABC", 50),
        };
        var recorder = new BotProxy();
        await InvokeAsync(CreateHandler(service), recorder, "/sh create", ChatType.Group);

        var request = Assert.IsType<SendMessageRequest>(recorder.Requests.Single());
        Assert.Equal("sh:err.not_enough_coins", request.Text);
    }

    [Fact]
    public async Task StatusWithoutGame_ReturnsError()
    {
        var service = new SecretHitlerServiceStub();
        var recorder = new BotProxy();
        await InvokeAsync(CreateHandler(service), recorder, "/sh status", ChatType.Private);

        var request = Assert.IsType<SendMessageRequest>(recorder.Requests.Single());
        Assert.Equal("sh:err.not_in_game", request.Text);
        Assert.Equal(1, service.FindCalls);
    }

    [Fact]
    public async Task JoinSuccess_ReportsUppercaseCodeAndSeatCount()
    {
        var service = new SecretHitlerServiceStub
        {
            JoinResult = new ShJoinResult(ShError.None, null, 3, 7),
        };
        var recorder = new BotProxy();
        await InvokeAsync(CreateHandler(service), recorder, "/sh join abc", ChatType.Private);

        var request = Assert.IsType<SendMessageRequest>(recorder.Requests.Single());
        Assert.Contains("ABC", request.Text, StringComparison.Ordinal);
        Assert.Contains("3/7", request.Text, StringComparison.Ordinal);
        Assert.Equal(1, service.JoinCalls);
    }

    [Fact]
    public async Task LeaveSuccess_ReportsLeaving()
    {
        var service = new SecretHitlerServiceStub
        {
            LeaveResult = new ShLeaveResult(ShError.None, null, false),
        };
        var recorder = new BotProxy();
        await InvokeAsync(CreateHandler(service), recorder, "/sh leave", ChatType.Group);

        var request = Assert.IsType<SendMessageRequest>(recorder.Requests.Single());
        Assert.Equal("sh:left", request.Text);
    }

    [Fact]
    public async Task InvalidCallback_IsAcknowledged()
    {
        var recorder = new BotProxy();
        await InvokeCallbackAsync(CreateHandler(new SecretHitlerServiceStub()), recorder, "stale");

        var request = Assert.IsType<AnswerCallbackQueryRequest>(recorder.Requests.Single());
        Assert.Equal("callback-1", request.CallbackQueryId);
        Assert.Null(request.Text);
    }

    [Fact]
    public async Task VoteError_IsAcknowledgedAndReported()
    {
        var service = new SecretHitlerServiceStub
        {
            VoteResult = new ShVoteResult(ShError.AlreadyVoted, null, null),
        };
        var recorder = new BotProxy();
        await InvokeCallbackAsync(CreateHandler(service), recorder, "sh:vote:ja");

        Assert.IsType<AnswerCallbackQueryRequest>(recorder.Requests[0]);
        var error = Assert.IsType<SendMessageRequest>(recorder.Requests[1]);
        Assert.Equal("sh:err.already_voted", error.Text);
        Assert.Equal(1, service.VoteCalls);
    }

    private static SecretHitlerHandler CreateHandler(SecretHitlerServiceStub service) => new(
        service,
        new EchoLocalizer(),
        Options.Create(new SecretHitlerOptions { BuyIn = 50 }),
        NullLogger<SecretHitlerHandler>.Instance);

    private static async Task InvokeAsync(
        SecretHitlerHandler handler,
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

    private static async Task InvokeCallbackAsync(SecretHitlerHandler handler, BotProxy recorder, string data)
    {
        var update = new Update
        {
            Id = 1,
            CallbackQuery = new CallbackQuery
            {
                Id = "callback-1",
                Data = data,
                From = new User { Id = 42, FirstName = "Alice" },
                Message = new Message
                {
                    Id = 9,
                    Chat = new Chat { Id = 100, Type = ChatType.Group },
                    Date = DateTime.UtcNow,
                },
            },
        };

        await handler.HandleAsync(new UpdateContext(recorder.Create(), update, null!, CancellationToken.None));
    }

    private sealed class SecretHitlerServiceStub : ISecretHitlerService
    {
        public ShCreateResult CreateResult { get; init; } = new(ShError.None, "ABC", 50);
        public ShJoinResult JoinResult { get; init; } = new(ShError.None, null, 1, 7);
        public ShVoteResult VoteResult { get; init; } = new(ShError.None, null, null);
        public ShLeaveResult LeaveResult { get; init; } = new(ShError.NotInGame, null, false);
        public int FindCalls { get; private set; }
        public int JoinCalls { get; private set; }
        public int VoteCalls { get; private set; }

        public Task<(ShGameSnapshot? Snapshot, SecretHitlerPlayer? Me)> FindMyGameAsync(long userId, CancellationToken ct)
        {
            FindCalls++;
            return Task.FromResult<(ShGameSnapshot?, SecretHitlerPlayer?)>((null, null));
        }

        public Task<ShCreateResult> CreateGameAsync(long userId, string displayName, long publicChatId, long playerChatId, CancellationToken ct) =>
            Task.FromResult(CreateResult);

        public Task<ShJoinResult> JoinGameAsync(long userId, string displayName, long playerChatId, string code, CancellationToken ct)
        {
            JoinCalls++;
            return Task.FromResult(JoinResult);
        }

        public Task<ShStartResult> StartGameAsync(long userId, CancellationToken ct) =>
            Task.FromResult(new ShStartResult(ShError.NotInGame, null));

        public Task<ShNominateResult> NominateAsync(long userId, int chancellorPosition, CancellationToken ct) =>
            Task.FromResult(new ShNominateResult(ShError.NotInGame, null));

        public Task<ShVoteResult> VoteAsync(long userId, ShVote vote, CancellationToken ct)
        {
            VoteCalls++;
            return Task.FromResult(VoteResult);
        }

        public Task<ShDiscardResult> PresidentDiscardAsync(long userId, int discardIndex, CancellationToken ct) =>
            Task.FromResult(new ShDiscardResult(ShError.NotInGame, null));

        public Task<ShEnactResult> ChancellorEnactAsync(long userId, int enactIndex, CancellationToken ct) =>
            Task.FromResult(new ShEnactResult(ShError.NotInGame, null, null));

        public Task<ShLeaveResult> LeaveAsync(long userId, CancellationToken ct) => Task.FromResult(LeaveResult);
        public Task SetStateMessageIdAsync(long userId, int messageId, CancellationToken ct) => Task.CompletedTask;
        public Task SetPublicStateMessageIdAsync(string inviteCode, int messageId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class EchoLocalizer : ILocalizer
    {
        public string Get(string moduleId, string key, string cultureCode = "ru") =>
            key == "joined" ? "{0} {1}/{2}" : $"{moduleId}:{key}";
        public string GetPlural(string moduleId, string key, int count, string cultureCode = "ru") => Get(moduleId, key, cultureCode);
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
