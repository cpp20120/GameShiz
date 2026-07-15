using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.RateLimiting;

namespace BotFramework.Rest.RateLimiting;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    public bool Enabled { get; init; } = true;
    public string? RedisConnectionString { get; set; }
    public string RedisKeyPrefix { get; init; } = "botframework:rate:v1";
    public int LocalMaxKeys { get; init; } = 100_000;
    public string PolicyVersion { get; init; } = "deployment-1";

    public RateLimitPolicy Tenant { get; init; } = RateLimitPolicy.PerMinute(3_000);
    public RateLimitPolicy RestUser { get; init; } = RateLimitPolicy.PerMinute(120);
    public RateLimitPolicy RestIp { get; init; } = RateLimitPolicy.PerMinute(300);
    public RateLimitPolicy TelegramUser { get; init; } = new(10, 1);
    public RateLimitPolicy DiscordUser { get; init; } = new(8, .8);
    public RateLimitPolicy Route { get; init; } = RateLimitPolicy.PerMinute(3_000);

    public RateLimitPolicySet Deployment(BotChannel channel) => new(
        Tenant,
        User(channel),
        RestIp,
        Route,
        Route,
        PolicyVersion);

    public RateLimitPolicy User(BotChannel channel) => channel switch
    {
        BotChannel.Telegram => TelegramUser,
        BotChannel.Discord => DiscordUser,
        _ => RestUser,
    };
}
