using System.Reflection;
using Games.Meta.Application.Risk;
using Games.Meta.Domain.Risk;
using Games.Meta.Domain.Seasons;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class MetaRiskHandlerTests
{
    [Theory]
    [InlineData("/risk")]
    [InlineData("/risk list")]
    public async Task List_WhenEmpty_ReturnsEmptyMessage(string command)
    {
        var service = new RiskServiceStub();
        var request = await InvokeAsync(service, command);

        Assert.Equal(100, service.LastChatId);
        Assert.Equal(20, service.LastLimit);
        Assert.Contains("нет", request.Text, StringComparison.Ordinal);
        Assert.Equal(ParseMode.Html, request.ParseMode);
    }

    [Fact]
    public async Task List_EncodesStoredRiskContent()
    {
        var service = new RiskServiceStub
        {
            Rows = [new RiskFlagView(7, 100, 42, "<Alice>", "payout<script>", "high&urgent", "open", "x < y", DateTimeOffset.UnixEpoch)],
        };

        var request = await InvokeAsync(service, "/risk list");

        Assert.Contains("&lt;Alice&gt;", request.Text, StringComparison.Ordinal);
        Assert.Contains("payout&lt;script&gt;", request.Text, StringComparison.Ordinal);
        Assert.Contains("high&amp;urgent", request.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", request.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("resolve", true, "✅")]
    [InlineData("ignore", false, "❌")]
    public async Task Mutation_UsesRequestedStatusAndReportsResult(string status, bool updated, string marker)
    {
        var service = new RiskServiceStub { UpdateResult = new RiskResolveResult(updated, "result <text>") };

        var request = await InvokeAsync(service, $"/risk {status} 77");

        Assert.Equal(77, service.LastFlagId);
        Assert.Equal(status, service.LastStatus);
        Assert.Contains(marker, request.Text, StringComparison.Ordinal);
        Assert.Contains("result &lt;text&gt;", request.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidCommand_ReturnsUsageWithoutMutating()
    {
        var service = new RiskServiceStub();

        var request = await InvokeAsync(service, "/risk resolve nope");

        Assert.Equal(0, service.UpdateCalls);
        Assert.Contains("/risk resolve", request.Text, StringComparison.Ordinal);
    }

    private static async Task<SendMessageRequest> InvokeAsync(RiskServiceStub service, string text)
    {
        var recorder = new BotProxy();
        var bot = recorder.Create();
        var update = new Update
        {
            Id = 1,
            Message = new Message
            {
                Id = 9,
                Text = text,
                From = new User { Id = 42, FirstName = "Admin" },
                Chat = new Chat { Id = 100, Type = ChatType.Private },
                Date = DateTime.UtcNow,
            },
        };
        await new RiskHandler(service).HandleAsync(new UpdateContext(bot, update, null!, CancellationToken.None));
        return Assert.IsType<SendMessageRequest>(recorder.Requests.Single());
    }

    private sealed class RiskServiceStub : IRiskService
    {
        public IReadOnlyList<RiskFlagView> Rows { get; init; } = [];
        public RiskResolveResult UpdateResult { get; init; } = new(false, "no");
        public long LastChatId { get; private set; }
        public int LastLimit { get; private set; }
        public long LastFlagId { get; private set; }
        public string? LastStatus { get; private set; }
        public int UpdateCalls { get; private set; }

        public Task EvaluateGameCompletedAsync(GameCompletedMetaEvent ev, SeasonPlayer player, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<RiskFlagView>> GetOpenAsync(long chatId, int limit, CancellationToken ct)
        {
            LastChatId = chatId;
            LastLimit = limit;
            return Task.FromResult(Rows);
        }
        public Task<RiskResolveResult> UpdateStatusAsync(long flagId, string status, CancellationToken ct)
        {
            UpdateCalls++;
            LastFlagId = flagId;
            LastStatus = status;
            return Task.FromResult(UpdateResult);
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
            if (method?.Name == "SendRequest" && args?[0] is object request) Current.Value?.Add(request);
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
