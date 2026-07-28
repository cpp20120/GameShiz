using System.Reflection;
using BotFramework.Host.Contracts.ResponsibleGaming;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class MetaMyStatsHandlerTests
{
    [Fact]
    public async Task Show_RendersStatsAndTracksAction()
    {
        var protection = new ProtectionStub
        {
            Stats = new PlayerStats(
                42,
                100,
                1_234,
                170,
                200,
                600,
                500,
                40,
                100,
                DateTimeOffset.UtcNow.AddHours(2),
                DateTimeOffset.UtcNow.AddDays(10)),
        };
        var analytics = new RecordingAnalyticsService();
        var request = await InvokeAsync("/mystats", ChatType.Private, protection, analytics);

        Assert.Contains("Моя статистика", request.Text, StringComparison.Ordinal);
        Assert.Contains("Баланс в этом чате: <b>1234</b>", request.Text, StringComparison.Ordinal);
        Assert.Contains("итог <b>+30</b>", request.Text, StringComparison.Ordinal);
        Assert.Contains("итог <b>-100</b>", request.Text, StringComparison.Ordinal);
        Assert.Contains("40/100 монет", request.Text, StringComparison.Ordinal);
        Assert.Contains("Перерыв: <b>до", request.Text, StringComparison.Ordinal);
        Assert.Contains("Самоисключение: <b>до", request.Text, StringComparison.Ordinal);
        Assert.Equal(ParseMode.Html, request.ParseMode);
        Assert.Single(analytics.Events);
        Assert.Equal("responsible_gaming", analytics.Events[0].ModuleId);
        Assert.Equal("settings_viewed", analytics.Events[0].EventName);
        Assert.Equal("show", analytics.Events[0].Tags["action"]);
    }

    [Fact]
    public async Task Limit_UpdatesAndCanBeDisabled()
    {
        var protection = new ProtectionStub();
        var analytics = new RecordingAnalyticsService();

        var setRequest = await InvokeAsync("/mystats limit 500", ChatType.Private, protection, analytics);
        Assert.Contains("установлен", setRequest.Text, StringComparison.Ordinal);
        Assert.Equal(500, protection.DailyLimit);

        var offRequest = await InvokeAsync("/mystats limit off", ChatType.Private, protection, analytics);
        Assert.Contains("отключён", offRequest.Text, StringComparison.Ordinal);
        Assert.Null(protection.DailyLimit);
    }

    [Theory]
    [InlineData("/mystats limit", "Использование")]
    [InlineData("/mystats limit -1", "Укажи целое число")]
    [InlineData("/mystats limit nope", "Укажи целое число")]
    [InlineData("/mystats cooldown 30m", "Допустимый перерыв")]
    [InlineData("/mystats cooldown 31d", "Допустимый перерыв")]
    [InlineData("/mystats cooldown xx", "Допустимый перерыв")]
    [InlineData("/mystats exclude 6d", "Самоисключение")]
    [InlineData("/mystats exclude 3651d", "Самоисключение")]
    public async Task InvalidSettings_ReturnUsageOrValidationError(string command, string expected)
    {
        var protection = new ProtectionStub();
        var request = await InvokeAsync(command, ChatType.Private, protection, new RecordingAnalyticsService());

        Assert.Contains(expected, request.Text, StringComparison.Ordinal);
        Assert.Null(protection.DailyLimit);
        Assert.Null(protection.CooldownUntil);
        Assert.Null(protection.SelfExcludedUntil);
    }

    [Fact]
    public async Task CooldownAndExclusion_StoreRequestedDeadlines()
    {
        var protection = new ProtectionStub();

        await InvokeAsync("/mystats cooldown 2h", ChatType.Private, protection, new RecordingAnalyticsService());
        Assert.NotNull(protection.CooldownUntil);
        Assert.InRange(protection.CooldownUntil!.Value, DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddHours(3));

        await InvokeAsync("/mystats exclude 7d", ChatType.Private, protection, new RecordingAnalyticsService());
        Assert.NotNull(protection.SelfExcludedUntil);
        Assert.InRange(protection.SelfExcludedUntil!.Value, DateTimeOffset.UtcNow.AddDays(6), DateTimeOffset.UtcNow.AddDays(8));
    }

    [Fact]
    public async Task GroupChat_IsRejectedWithoutTracking()
    {
        var analytics = new RecordingAnalyticsService();
        var request = await InvokeAsync("/mystats", ChatType.Group, new ProtectionStub(), analytics);

        Assert.Contains("только в личном чате", request.Text, StringComparison.Ordinal);
        Assert.Empty(analytics.Events);
    }

    [Fact]
    public async Task UnknownAction_ReturnsUsage()
    {
        var request = await InvokeAsync("/mystats something", ChatType.Private, new ProtectionStub(), new RecordingAnalyticsService());

        Assert.Contains("Использование", request.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingUserOrMessage_IsIgnored()
    {
        var recorder = new BotProxy();
        var handler = new MyStatsHandler(new ProtectionStub(), new RecordingAnalyticsService());

        await handler.HandleAsync(new UpdateContext(
            recorder.Create(),
            new Update
            {
                Id = 1,
                Message = new Message
                {
                    Id = 9,
                    Text = "/mystats",
                    Chat = new Chat { Id = 100, Type = ChatType.Private },
                    Date = DateTime.UtcNow,
                },
            },
            null!,
            CancellationToken.None));

        Assert.Empty(recorder.Requests);
    }

    private static async Task<SendMessageRequest> InvokeAsync(
        string text,
        ChatType chatType,
        ProtectionStub protection,
        RecordingAnalyticsService analytics)
    {
        var recorder = new BotProxy();
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

        await new MyStatsHandler(protection, analytics).HandleAsync(
            new UpdateContext(recorder.Create(), update, null!, CancellationToken.None));

        return Assert.IsType<SendMessageRequest>(recorder.Requests.Single());
    }

    private sealed class ProtectionStub : IPlayerProtectionService
    {
        public PlayerStats Stats { get; init; } = new(42, 100, 0, 0, 0, 0, 0, 0, null, null, null);
        public int? DailyLimit { get; private set; }
        public DateTimeOffset? CooldownUntil { get; private set; }
        public DateTimeOffset? SelfExcludedUntil { get; private set; }

        public Task<PlayerStats> GetStatsAsync(long userId, long balanceScopeId, CancellationToken ct) => Task.FromResult(Stats);

        public Task SetDailyLimitAsync(long userId, int? limit, CancellationToken ct)
        {
            DailyLimit = limit;
            return Task.CompletedTask;
        }

        public Task SetCooldownAsync(long userId, DateTimeOffset until, CancellationToken ct)
        {
            CooldownUntil = until;
            return Task.CompletedTask;
        }

        public Task SetSelfExclusionAsync(long userId, DateTimeOffset until, CancellationToken ct)
        {
            SelfExcludedUntil = until;
            return Task.CompletedTask;
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
