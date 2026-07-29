using ChatAdministration.Domain.Models;
using ChatAdministration.Telegram.Infrastructure;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

public sealed class RedisModerationRateLimitPropertyTests
{
    [Property(MaxTest = 250)]
    public Property FallbackCountersRemainMonotonicAndDoNotStoreMessageText(NonNegativeInt raw)
    {
        var store = new RedisModerationRateLimitStore();
        var now = DateTimeOffset.UtcNow;
        var message = new NormalizedMessage
        {
            ChatId = new ChatId(-100),
            MessageId = Math.Max(1, raw.Get % 1_000_000),
            AuthorId = new UserId(20),
            Text = $"SECRET-{raw.Get}",
            Entities = [new MessageEntity(MessageEntityType.Url, 0, 1, "https://example.com")],
            ContentType = MessageContentType.Text,
            SentAt = now,
        };

        var first = store.RecordAsync(message, TimeSpan.FromMinutes(1)).AsTask().GetAwaiter().GetResult();
        var second = store.RecordAsync(message with { MessageId = message.MessageId + 1 }, TimeSpan.FromMinutes(1)).AsTask().GetAwaiter().GetResult();

        return (second.RateLimits.MessagesInWindow == first.RateLimits.MessagesInWindow + 1
                && second.RateLimits.LinksInWindow == first.RateLimits.LinksInWindow + 1
                && second.History.RecentMessageHashes.All(hash => !hash.Contains("SECRET", StringComparison.Ordinal)))
            .ToProperty();
    }
}
