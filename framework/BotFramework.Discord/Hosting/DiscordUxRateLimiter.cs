using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace BotFramework.Discord.Hosting;

public sealed class DiscordUxRateLimiter
{
    private readonly DiscordOptions _options;
    private readonly TimeProvider _clock;
    private readonly ConcurrentDictionary<string, Bucket> _buckets = new(StringComparer.Ordinal);

    public DiscordUxRateLimiter(IOptions<DiscordOptions> options, TimeProvider? clock = null)
    {
        _options = options.Value;
        _clock = clock ?? TimeProvider.System;
    }

    public DiscordUxDecision Check(
        ulong userId,
        string bucketName,
        bool autocomplete = false,
        bool interaction = true)
    {
        if (autocomplete) return new DiscordUxDecision(true, TimeSpan.Zero);

        var now = _clock.GetUtcNow();
        var bucket = _buckets.GetOrAdd($"{userId}:{bucketName}", _ => new Bucket());
        lock (bucket)
        {
            var window = TimeSpan.FromSeconds(Math.Max(1, _options.RateLimitWindowSeconds));
            while (bucket.Requests.Count > 0 && now - bucket.Requests.Peek() >= window)
                bucket.Requests.Dequeue();

            var cooldown = TimeSpan.FromMilliseconds(Math.Max(
                0,
                interaction ? _options.InteractionCooldownMilliseconds : _options.CommandCooldownMilliseconds));
            if (bucket.LastAccepted is { } last && now - last < cooldown)
                return Denied(cooldown - (now - last));

            if (bucket.Requests.Count >= Math.Max(1, _options.RateLimitMaxRequests))
                return Denied(window - (now - bucket.Requests.Peek()));

            bucket.LastAccepted = now;
            bucket.Requests.Enqueue(now);
            return new DiscordUxDecision(true, TimeSpan.Zero);
        }
    }

    public void Clear() => _buckets.Clear();

    private static DiscordUxDecision Denied(TimeSpan retryAfter) =>
        new(false, retryAfter < TimeSpan.Zero ? TimeSpan.Zero : retryAfter);

    private sealed class Bucket
    {
        public Queue<DateTimeOffset> Requests { get; } = new();
        public DateTimeOffset? LastAccepted { get; set; }
    }
}
