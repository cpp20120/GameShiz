using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Telegram.Infrastructure;

public sealed class InMemoryModerationRateLimitStore : IModerationRateLimitStore
{
    private readonly ConcurrentDictionary<(long ChatId, long UserId), Queue<ModerationRateLimitEntry>> observations = new();

    private ModerationRateObservation RecordCore(NormalizedMessage message, TimeSpan window)
    {
        var key = (message.ChatId.Value, message.AuthorId.Value);
        var queue = observations.GetOrAdd(key, static _ => new Queue<ModerationRateLimitEntry>());
        var now = message.SentAt == default ? DateTimeOffset.UtcNow : message.SentAt;
        lock (queue)
        {
            while (queue.TryPeek(out var oldest) && now - oldest.SentAt > window)
                queue.Dequeue();

            var hash = Hash(message.Text);
            queue.Enqueue(new ModerationRateLimitEntry(now, hash, message.Entities.Any(entity => entity.Type is MessageEntityType.Url or MessageEntityType.TextLink), message.Entities.Count(entity => entity.Type == MessageEntityType.BotCommand)));
            var snapshot = queue.ToArray();
            return new ModerationRateObservation(
                new RateLimitSnapshot
                {
                    MessagesInWindow = snapshot.Length,
                    LinksInWindow = snapshot.Count(item => item.HasLink),
                    CommandsInWindow = snapshot.Sum(item => item.CommandCount),
                },
                new ModerationHistorySummary { RecentMessageHashes = snapshot.TakeLast(20).Select(item => item.Hash).ToArray() });
        }
    }

    public ValueTask<ModerationRateObservation> RecordAsync(
        NormalizedMessage message,
        TimeSpan window,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(RecordCore(message, window));

    private static string Hash(string? text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(' ', (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant())));
}
