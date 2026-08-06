using Microsoft.Extensions.DependencyInjection;
using BotFramework.Contracts.Messaging;
using Xunit;

namespace CasinoShiz.Tests;

public class CommandBusTests
{
    // ── Fakes ────────────────────────────────────────────────────────────────

    private sealed record PingCommand(string ModuleId = "test") : ICommand;
    private sealed record EchoCommand(string Payload, string ModuleId = "test") : ICommand<string>;

    private sealed class PingHandler : ICommandHandler<PingCommand>
    {
        public bool Called { get; private set; }
        public Task HandleAsync(PingCommand command, CancellationToken ct)
        {
            Called = true;
            return Task.CompletedTask;
        }
    }

    private sealed class EchoHandler : ICommandHandler<EchoCommand, string>
    {
        public Task<string> HandleAsync(EchoCommand command, CancellationToken ct)
            => Task.FromResult(command.Payload.ToUpperInvariant());
    }

    private sealed class CapturingMiddleware : ICommandMiddleware
    {
        public List<string> Order { get; } = [];
        public async Task InvokeAsync(CommandContext ctx, Func<Task> next)
        {
            Order.Add("before");
            await next();
            Order.Add("after");
        }
    }

    private sealed class RequestCapturingMiddleware : ICommandMiddleware
    {
        public RequestMetadata? Request { get; private set; }

        public async Task InvokeAsync(CommandContext ctx, Func<Task> next)
        {
            Request = ctx.Request;
            await next();
        }
    }

    private static IServiceProvider BuildServices(Action<IServiceCollection>? extra = null)
    {
        var sc = new ServiceCollection();
        sc.AddScoped<ICommandHandler<PingCommand>, PingHandler>();
        sc.AddScoped<ICommandHandler<EchoCommand, string>, EchoHandler>();
        extra?.Invoke(sc);
        return sc.BuildServiceProvider();
    }

    private static CommandBus MakeBus(IServiceProvider sp, params ICommandMiddleware[] middleware)
        => new(sp, middleware);

    // ── SendAsync (no result) ────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_CallsHandler()
    {
        await using var sp = (ServiceProvider)BuildServices();
        var bus = MakeBus(sp);
        await bus.SendAsync(new PingCommand(), default);
        var handler = (PingHandler)sp.GetRequiredService<ICommandHandler<PingCommand>>();
        Assert.True(handler.Called);
    }

    [Fact]
    public async Task SendAsync_NoHandlerRegistered_Throws()
    {
        var sc = new ServiceCollection();
        await using var sp = sc.BuildServiceProvider();
        var bus = MakeBus(sp);
        await Assert.ThrowsAsync<InvalidOperationException>(() => bus.SendAsync(new PingCommand(), default));
    }

    // ── SendAsync<TResult> ───────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_WithResult_ReturnsHandlerResult()
    {
        await using var sp = (ServiceProvider)BuildServices();
        var bus = MakeBus(sp);
        var result = await bus.SendAsync<string>(new EchoCommand("hello"), default);
        Assert.Equal("HELLO", result);
    }

    [Fact]
    public async Task SendAsync_WithResult_DifferentPayloads()
    {
        await using var sp = (ServiceProvider)BuildServices();
        var bus = MakeBus(sp);
        var r1 = await bus.SendAsync<string>(new EchoCommand("foo"), default);
        var r2 = await bus.SendAsync<string>(new EchoCommand("bar"), default);
        Assert.Equal("FOO", r1);
        Assert.Equal("BAR", r2);
    }

    // ── Middleware chain ─────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_SingleMiddleware_ExecutesAroundHandler()
    {
        await using var sp = (ServiceProvider)BuildServices();
        var mw = new CapturingMiddleware();
        var bus = MakeBus(sp, mw);
        await bus.SendAsync(new PingCommand(), default);
        Assert.Equal(["before", "after"], mw.Order);
    }

    [Fact]
    public async Task SendAsync_MultipleMiddleware_ExecutesInOrder()
    {
        await using var sp = (ServiceProvider)BuildServices();
        var order = new List<string>();
        var mw1 = new OrderedMiddleware("mw1", order);
        var mw2 = new OrderedMiddleware("mw2", order);
        var bus = MakeBus(sp, mw1, mw2);
        await bus.SendAsync(new PingCommand(), default);
        Assert.Equal(["mw1:before", "mw2:before", "mw2:after", "mw1:after"], order);
    }

    [Fact]
    public async Task SendAsync_MiddlewareShortCircuits_HandlerNotCalled()
    {
        await using var sp = (ServiceProvider)BuildServices();
        var bus = MakeBus(sp, new ShortCircuitMiddleware());
        // Should complete without calling handler — no throw
        var ex = await Record.ExceptionAsync(() => bus.SendAsync(new PingCommand(), default));
        Assert.Null(ex);
        var handler = (PingHandler)sp.GetRequiredService<ICommandHandler<PingCommand>>();
        Assert.False(handler.Called);
    }

    // ── RequestMetadata command context ─────────────────────────────────────

    [Fact]
    public async Task CommandBus_DefaultsToSystemRequestMetadata()
    {
        await using var sp = (ServiceProvider)BuildServices();
        var capture = new RequestCapturingMiddleware();
        var bus = MakeBus(sp, capture);

        await bus.SendAsync(new PingCommand(), default);

        Assert.Equal(BotChannel.System, capture.Request?.Channel);
    }

    [Fact]
    public async Task CommandBus_UsesAmbientRequestMetadata()
    {
        await using var sp = (ServiceProvider)BuildServices();
        var capture = new RequestCapturingMiddleware();
        var bus = MakeBus(sp, capture);
        var metadata = RequestMetadata.Create(
            "telegram",
            userId: "42",
            culture: "ru",
            channel: BotChannel.Telegram);

        using var scope = RequestMetadataContext.Push(metadata);
        await bus.SendAsync(new PingCommand(), default);

        Assert.Same(metadata, capture.Request);
    }

    private sealed class OrderedMiddleware(string name, List<string> order) : ICommandMiddleware
    {
        public async Task InvokeAsync(CommandContext ctx, Func<Task> next)
        {
            order.Add($"{name}:before");
            await next();
            order.Add($"{name}:after");
        }
    }

    private sealed class ShortCircuitMiddleware : ICommandMiddleware
    {
        public Task InvokeAsync(CommandContext ctx, Func<Task> next) => Task.CompletedTask;
    }
}
