using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Xunit;
using LoggingMiddleware = BotFramework.Host.Pipeline.Middleware.LoggingMiddleware;
using RateLimitMiddleware = BotFramework.Host.Pipeline.Middleware.RateLimitMiddleware;

namespace CasinoShiz.Tests;

public class FrameworkMiddlewareTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static UpdateContext MakeCtx(long userId = 1, string? text = null, string? cbData = null)
    {
        Update update;
        if (cbData != null)
        {
            update = new Update
            {
                Id = 1,
                CallbackQuery = new CallbackQuery
                {
                    Id = "1",
                    Data = cbData,
                    From = new User { Id = userId, IsBot = false, FirstName = "T" },
                },
            };
        }
        else
        {
            update = new Update
            {
                Id = 1,
                Message = new Message
                {
                    Id = 1,
                    Text = text ?? "hi",
                    From = new User { Id = userId, IsBot = false, FirstName = "T" },
                    Chat = new Chat { Id = 1, Type = ChatType.Private },
                    Date = DateTime.UtcNow,
                },
            };
        }
        return new UpdateContext(null!, update, null!, default);
    }

    // ── LoggingMiddleware ────────────────────────────────────────────────────

    [Fact]
    public async Task LoggingMiddleware_CallsNext()
    {
        var mw = new LoggingMiddleware(NullLogger<LoggingMiddleware>.Instance);
        var called = false;
        await mw.InvokeAsync(MakeCtx(), _ => { called = true; return Task.CompletedTask; });
        Assert.True(called);
    }

    [Fact]
    public async Task LoggingMiddleware_NextThrows_RethrowsException()
    {
        var mw = new LoggingMiddleware(NullLogger<LoggingMiddleware>.Instance);
        Task Next(UpdateContext _) => throw new InvalidOperationException("test error");
        await Assert.ThrowsAsync<InvalidOperationException>(() => mw.InvokeAsync(MakeCtx(), Next));
    }

    [Fact]
    public async Task LoggingMiddleware_TextUpdate_DoesNotThrow()
    {
        var mw = new LoggingMiddleware(NullLogger<LoggingMiddleware>.Instance);
        var ex = await Record.ExceptionAsync(() => mw.InvokeAsync(MakeCtx(text: "/poker create"), _ => Task.CompletedTask));
        Assert.Null(ex);
    }

    [Fact]
    public async Task LoggingMiddleware_CallbackUpdate_DoesNotThrow()
    {
        var mw = new LoggingMiddleware(NullLogger<LoggingMiddleware>.Instance);
        var ex = await Record.ExceptionAsync(() => mw.InvokeAsync(MakeCtx(cbData: "poker:check"), _ => Task.CompletedTask));
        Assert.Null(ex);
    }

    // ── ExceptionMiddleware ──────────────────────────────────────────────────

    [Fact]
    public async Task ExceptionMiddleware_NoException_CallsNext()
    {
        var mw = new ExceptionMiddleware(new NullAnalyticsService(), NullLogger<ExceptionMiddleware>.Instance);
        var called = false;
        await mw.InvokeAsync(MakeCtx(), _ => { called = true; return Task.CompletedTask; });
        Assert.True(called);
    }

    [Fact]
    public async Task ExceptionMiddleware_HandlerThrows_Rethrows()
    {
        var mw = new ExceptionMiddleware(new NullAnalyticsService(), NullLogger<ExceptionMiddleware>.Instance);
        Task Next(UpdateContext _) => throw new InvalidOperationException("game error");
        await Assert.ThrowsAsync<InvalidOperationException>(() => mw.InvokeAsync(MakeCtx(), Next));
    }

    [Fact]
    public async Task ExceptionMiddleware_HandlerThrows_TracksAnalytics()
    {
        var analytics = new TrackingAnalyticsService();
        var mw = new ExceptionMiddleware(analytics, NullLogger<ExceptionMiddleware>.Instance);
        Task Next(UpdateContext _) => throw new InvalidOperationException("game error");
        await Assert.ThrowsAsync<InvalidOperationException>(() => mw.InvokeAsync(MakeCtx(), Next));
        Assert.Single(analytics.Events);
    }

    [Fact]
    public async Task ExceptionMiddleware_CancellationRequested_Rethrows()
    {
        var mw = new ExceptionMiddleware(new NullAnalyticsService(), NullLogger<ExceptionMiddleware>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var update = new Update { Id = 1, Message = new Message { Id = 1, Text = "x", Chat = new Chat { Id = 1, Type = ChatType.Private }, Date = DateTime.UtcNow } };
        var ctx = new UpdateContext(null!, update, null!, cts.Token);
        Task Next(UpdateContext _) => throw new OperationCanceledException(cts.Token);
        await Assert.ThrowsAsync<OperationCanceledException>(() => mw.InvokeAsync(ctx, Next));
    }

    // ── UpdateAnalyticsMiddleware ───────────────────────────────────────────

    [Theory]
    [InlineData("/Dice@CasinoBot 25", "dice", true)]
    [InlineData("   /HELP", "help", false)]
    public async Task UpdateAnalytics_Command_NormalizesCommandAndReportsArguments(
        string text, string expectedCommand, bool expectedHasArgs)
    {
        var analytics = new TrackingAnalyticsService();
        var middleware = new UpdateAnalyticsMiddleware(analytics);

        await middleware.InvokeAsync(MakeCtx(userId: 42, text: text), _ => Task.CompletedTask);

        var tracked = Assert.Single(analytics.Events, x => x.EventName == "command");
        Assert.Equal(("telegram", "command"), (tracked.ModuleId, tracked.EventName));
        Assert.Equal(expectedCommand, tracked.Tags["command"]);
        Assert.Equal(expectedHasArgs, tracked.Tags["has_args"]);
        Assert.Equal(42L, tracked.Tags["user_id"]);
        Assert.Equal("private", tracked.Tags["chat_type"]);
    }

    [Fact]
    public async Task UpdateAnalytics_Text_ReportsLengthAndCallsNext()
    {
        var analytics = new TrackingAnalyticsService();
        var middleware = new UpdateAnalyticsMiddleware(analytics);
        var nextCalled = false;

        await middleware.InvokeAsync(MakeCtx(text: "hello"), _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var tracked = Assert.Single(analytics.Events, x => x.EventName == "message");
        Assert.True(nextCalled);
        Assert.Equal("message", tracked.EventName);
        Assert.Equal(5, tracked.Tags["text_length"]);
        Assert.Equal("text", tracked.Tags["kind"]);
    }

    [Theory]
    [InlineData("poker:check:now", "poker")]
    [InlineData("", "empty")]
    [InlineData("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ", "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUV")]
    public async Task UpdateAnalytics_Callback_ReportsBoundedPrefix(string data, string expectedPrefix)
    {
        var analytics = new TrackingAnalyticsService();
        var middleware = new UpdateAnalyticsMiddleware(analytics);

        await middleware.InvokeAsync(MakeCtx(cbData: data), _ => Task.CompletedTask);

        var tracked = Assert.Single(analytics.Events, x => x.EventName == "callback");
        Assert.Equal("callback", tracked.EventName);
        Assert.Equal(expectedPrefix, tracked.Tags["callback_prefix"]);
        Assert.Equal(data.Length, tracked.Tags["callback_length"]);
    }

    [Fact]
    public async Task UpdateAnalytics_Dice_ReportsEmojiAndValue()
    {
        var analytics = new TrackingAnalyticsService();
        var middleware = new UpdateAnalyticsMiddleware(analytics);
        var update = new Update
        {
            Id = 7,
            Message = new Message
            {
                Id = 8,
                From = new User { Id = 9, IsBot = false, FirstName = "T" },
                Chat = new Chat { Id = 10, Type = ChatType.Group },
                Date = DateTime.UtcNow,
                Dice = new Dice { Emoji = "🎯", Value = 6 },
            },
        };

        await middleware.InvokeAsync(new UpdateContext(null!, update, null!, default), _ => Task.CompletedTask);

        var tracked = Assert.Single(analytics.Events, x => x.EventName == "dice");
        Assert.Equal("dice", tracked.EventName);
        Assert.Equal("🎯", tracked.Tags["emoji"]);
        Assert.Equal(6, tracked.Tags["value"]);
        Assert.Equal("group", tracked.Tags["chat_type"]);
    }

    [Fact]
    public async Task UpdateAnalytics_UnknownUpdate_UsesGenericEvent()
    {
        var analytics = new TrackingAnalyticsService();
        var middleware = new UpdateAnalyticsMiddleware(analytics);

        await middleware.InvokeAsync(
            new UpdateContext(null!, new Update { Id = 12 }, null!, default),
            _ => Task.CompletedTask);

        var tracked = Assert.Single(analytics.Events, x => x.EventName == "update");
        Assert.Equal("update", tracked.EventName);
        Assert.Null(tracked.Tags["chat_type"]);
    }

    // ── RateLimitMiddleware ──────────────────────────────────────────────────

    [Fact]
    public async Task RateLimit_FirstRequest_PassesThrough()
    {
        var mw = new RateLimitMiddleware(NullLogger<RateLimitMiddleware>.Instance);
        var called = false;
        // Use large unique userId to avoid sharing bucket state across tests
        await mw.InvokeAsync(MakeCtx(userId: 9_000_001), _ => { called = true; return Task.CompletedTask; });
        Assert.True(called);
    }

    [Fact]
    public async Task RateLimit_ZeroUserId_AlwaysPassesThrough()
    {
        var mw = new RateLimitMiddleware(NullLogger<RateLimitMiddleware>.Instance);
        // userId=0 (no user) bypasses rate limiting
        var callCount = 0;
        for (var i = 0; i < 20; i++)
        {
            await mw.InvokeAsync(MakeCtx(userId: 0), _ => { callCount++; return Task.CompletedTask; });
        }
        Assert.Equal(20, callCount);
    }

    [Fact]
    public async Task RateLimit_BurstWithinCapacity_AllPassThrough()
    {
        var mw = new RateLimitMiddleware(NullLogger<RateLimitMiddleware>.Instance);
        const long userId = 9_000_002L;
        var callCount = 0;
        // Capacity is 10 — first 10 requests should all pass
        for (var i = 0; i < 10; i++)
        {
            await mw.InvokeAsync(MakeCtx(userId: userId), _ => { callCount++; return Task.CompletedTask; });
        }
        Assert.Equal(10, callCount);
    }

    [Fact]
    public async Task RateLimit_ExceedCapacity_DropsRequests()
    {
        var mw = new RateLimitMiddleware(NullLogger<RateLimitMiddleware>.Instance);
        const long userId = 9_000_003L;
        var callCount = 0;
        // Send 15 requests — capacity is 10, so at least some should be dropped
        for (var i = 0; i < 15; i++)
        {
            await mw.InvokeAsync(MakeCtx(userId: userId), _ => { callCount++; return Task.CompletedTask; });
        }
        Assert.True(callCount < 15, string.Create(CultureInfo.InvariantCulture, $"Expected some drops but got {callCount}/15 calls"));
    }

    // ── UpdatePipeline ────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePipeline_NoMiddleware_DoesNotThrow()
    {
        var router = new UpdateRouter([], NullLogger<UpdateRouter>.Instance);
        var pipeline = new UpdatePipeline([], router);
        var ex = await Record.ExceptionAsync(() => pipeline.InvokeAsync(MakeCtx()));
        Assert.Null(ex);
    }

    [Fact]
    public async Task UpdatePipeline_SingleMiddleware_BeforeAndAfterBothCalled()
    {
        var router = new UpdateRouter([], NullLogger<UpdateRouter>.Instance);
        var order = new List<string>();
        var mw = new OrderMiddleware("mw1", order);
        var pipeline = new UpdatePipeline([mw], router);
        await pipeline.InvokeAsync(MakeCtx());
        // before and after must both be present, in that order
        Assert.Equal(2, order.Count);
        Assert.Equal("mw1:before", order[0]);
        Assert.Equal("mw1:after", order[1]);
    }

    [Fact]
    public async Task UpdatePipeline_MultipleMiddleware_OuterWrapsInner()
    {
        var router = new UpdateRouter([], NullLogger<UpdateRouter>.Instance);
        var order = new List<string>();
        var pipeline = new UpdatePipeline(
            [new OrderMiddleware("mw1", order), new OrderMiddleware("mw2", order)],
            router);
        await pipeline.InvokeAsync(MakeCtx());
        // onion ordering: mw1 is outermost, mw2 is innermost
        Assert.Equal(["mw1:before", "mw2:before", "mw2:after", "mw1:after"], order);
    }

    [Fact]
    public async Task UpdatePipeline_MiddlewareShortCircuits_InnerNotCalled()
    {
        var router = new UpdateRouter([], NullLogger<UpdateRouter>.Instance);
        var order = new List<string>();
        // mw1 short-circuits (doesn't call next), mw2 should never execute
        var shortCircuit = new ShortCircuitMiddleware();
        var inner = new OrderMiddleware("mw2", order);
        var pipeline = new UpdatePipeline([shortCircuit, inner], router);
        await pipeline.InvokeAsync(MakeCtx());
        Assert.Empty(order);
    }

    private sealed class OrderMiddleware(string name, List<string> order) : IUpdateMiddleware
    {
        public async Task InvokeAsync(UpdateContext ctx, UpdateDelegate next)
        {
            order.Add($"{name}:before");
            await next(ctx);
            order.Add($"{name}:after");
        }
    }

    private sealed class ShortCircuitMiddleware : IUpdateMiddleware
    {
        public Task InvokeAsync(UpdateContext ctx, UpdateDelegate next) => Task.CompletedTask;
    }
}

// Tracking analytics for ExceptionMiddleware test
file sealed class TrackingAnalyticsService : IAnalyticsService
{
    public List<TrackedAnalyticsEvent> Events { get; } = [];
    public void Track(string moduleId, string eventName, IReadOnlyDictionary<string, object?> tags)
        => Events.Add(new TrackedAnalyticsEvent(moduleId, eventName, tags));
}

file sealed record TrackedAnalyticsEvent(
    string ModuleId,
    string EventName,
    IReadOnlyDictionary<string, object?> Tags);
