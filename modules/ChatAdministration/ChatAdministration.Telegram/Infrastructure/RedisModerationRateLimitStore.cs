using System.Security.Cryptography;
using System.Text;
using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ChatAdministration.Telegram.Infrastructure;

/// <summary>
/// Distributed moderation counters. One Lua invocation updates all counters
/// for a message, so flood decisions remain atomic when several bot replicas
/// process the same chat concurrently. The bounded in-memory store is used
/// only when Redis is not configured or temporarily unavailable.
/// </summary>
public sealed class RedisModerationRateLimitStore(
    IConnectionMultiplexer? redis = null,
    ILogger<RedisModerationRateLimitStore>? logger = null) : IModerationRateLimitStore
{
    private const string CounterScript = """
        local ttl = tonumber(ARGV[1])
        local hasLink = tonumber(ARGV[2])
        local commandCount = tonumber(ARGV[3])
        local messages = redis.call('INCR', KEYS[1])
        redis.call('EXPIRE', KEYS[1], ttl)
        local links = 0
        if hasLink == 1 then
          links = redis.call('INCR', KEYS[2])
          redis.call('EXPIRE', KEYS[2], ttl)
        end
        local commands = 0
        if commandCount > 0 then
          commands = redis.call('INCRBY', KEYS[3], commandCount)
          redis.call('EXPIRE', KEYS[3], ttl)
        end
        local duplicates = redis.call('INCR', KEYS[4])
        redis.call('EXPIRE', KEYS[4], ttl)
        return { messages, links, commands, duplicates }
        """;

    private readonly InMemoryModerationRateLimitStore fallback = new();

    public async ValueTask<ModerationRateObservation> RecordAsync(
        NormalizedMessage message,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        if (redis is null || !redis.IsConnected)
            return await fallback.RecordAsync(message, window, cancellationToken);

        try
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(window.TotalSeconds));
            var now = message.SentAt == default ? DateTimeOffset.UtcNow : message.SentAt;
            var bucket = now.ToUnixTimeSeconds() / seconds;
            var hash = Hash(message.Text);
            var hasLink = message.Entities.Any(entity => entity.Type is MessageEntityType.Url or MessageEntityType.TextLink);
            var commandCount = message.Entities.Count(entity => entity.Type == MessageEntityType.BotCommand);
            var prefix = $"moderation:{message.ChatId.Value}:{message.AuthorId.Value}:{bucket}";
            var keys = new RedisKey[]
            {
                $"{prefix}:messages",
                $"{prefix}:links",
                $"{prefix}:commands",
                $"{prefix}:duplicates:{hash}",
            };
            var result = (RedisResult[]?)await redis.GetDatabase().ScriptEvaluateAsync(
                CounterScript,
                keys,
                [seconds * 2 + 1, hasLink ? 1 : 0, commandCount],
                CommandFlags.DemandMaster);
            if (result is not { Length: 4 })
                throw new RedisException("Moderation counter script returned an invalid result.");

            return new ModerationRateObservation(
                new RateLimitSnapshot
                {
                    MessagesInWindow = Convert.ToInt32(result[0]),
                    LinksInWindow = Convert.ToInt32(result[1]),
                    CommandsInWindow = Convert.ToInt32(result[2]),
                },
                new ModerationHistorySummary
                {
                    RecentMessageHashes = [hash],
                });
        }
        catch (RedisException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger?.LogWarning(exception, "chat_admin.redis_rate_limit_fallback");
            return await fallback.RecordAsync(message, window, cancellationToken);
        }
    }

    private static string Hash(string? text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join(' ', (text ?? string.Empty)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.ToLowerInvariant())))));
}
