using BotFramework.Host.Contracts.Discord;
using BotFramework.Contracts.Messaging;
using BotFramework.Host.Contracts.Telegram;
using Games.Redeem.Application.Jobs;
using Games.Redeem.Application.Services;
using Games.Redeem.Domain.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class RedeemDropSubscriberTests
{
    [Fact]
    public async Task HandleAsync_EnqueuesTelegramOutboxMessage()
    {
        var code = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var redeem = new RecordingRedeemService(code);
        var outbox = new RecordingTelegramOutbox();

        var services = new ServiceCollection()
            .AddSingleton<IRedeemService>(redeem)
            .BuildServiceProvider();

        var subscriber = new RedeemDropSubscriber(
            services,
            outbox,
            new FakeLocalizer(),
            NullLogger<RedeemDropSubscriber>.Instance);

        var ev = new MiniGameRedeemCodeDropRequested(123, -456, "dice", 987654321, BotChannel.Telegram);

        await subscriber.HandleAsync(ev, CancellationToken.None);

        Assert.Equal("dice", redeem.FreeSpinGameIds.Single());
        var message = Assert.Single(outbox.Messages);
        Assert.Equal(-456, message.ChatId);
        Assert.Equal(OutboundParseMode.Html, message.ParseMode);
        Assert.Equal("redeem-drop:123:-456:dice:987654321", message.DedupeKey);
        Assert.Contains(code.ToString(), message.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_EnqueuesDiscordOutboxForDiscordEventOnly()
    {
        var code = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var redeem = new RecordingRedeemService(code);
        var outbox = new RecordingDiscordOutbox();
        var services = new ServiceCollection()
            .AddSingleton<IRedeemService>(redeem)
            .BuildServiceProvider();
        var subscriber = new DiscordRedeemDropSubscriber(
            services,
            outbox,
            NullLogger<DiscordRedeemDropSubscriber>.Instance);

        await subscriber.HandleAsync(
            new MiniGameRedeemCodeDropRequested(123, 456, "dice", 987654321, BotChannel.Discord),
            CancellationToken.None);

        var message = Assert.Single(outbox.Messages);
        Assert.Equal(456, message.ChannelId);
        Assert.Equal(123, message.UserId);
        Assert.Contains(code.ToString(), message.Text, StringComparison.Ordinal);
        Assert.Contains("discord", message.DedupeKey, StringComparison.Ordinal);
    }

    private sealed class RecordingTelegramOutbox : ITelegramOutbox
    {
        public List<TelegramOutboxMessage> Messages { get; } = [];

        public Task EnqueueAsync(TelegramOutboxMessage message, CancellationToken ct)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingDiscordOutbox : IDiscordOutbox
    {
        public List<DiscordOutboxMessage> Messages { get; } = [];

        public Task EnqueueAsync(DiscordOutboxMessage message, CancellationToken ct)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRedeemService(Guid code) : IRedeemService
    {
        public List<string?> FreeSpinGameIds { get; } = [];

        public Task<Guid> IssueAdminCodeAsync(long userId, CancellationToken ct, string? freeSpinGameId = null)
        {
            FreeSpinGameIds.Add(freeSpinGameId);
            return Task.FromResult(code);
        }

        public Task<BeginRedeemResult> BeginRedeemAsync(
            long userId,
            long balanceScopeId,
            string displayName,
            string codeText,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<CompleteRedeemResult> CompleteRedeemAsync(
            long userId,
            long balanceScopeId,
            Guid codeGuid,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public void ReportCaptcha(long userId, string codeText, string pattern, bool passed) =>
            throw new NotSupportedException();
    }

    private sealed class FakeLocalizer : ILocalizer
    {
        public string Get(string moduleId, string key, string cultureCode = "ru") =>
            "{0}";

        public string GetPlural(string moduleId, string key, int count, string cultureCode = "ru") =>
            "{0}";
    }
}
