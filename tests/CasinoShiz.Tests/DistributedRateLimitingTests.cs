using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.RateLimiting;
using BotFramework.Contracts.Tenancy;
using BotFramework.Host.RateLimiting;
using BotFramework.Telegram.Abstractions.Tenancy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class DistributedRateLimitingTests
{
    [Fact]
    public async Task LocalFallback_AppliesTenantLeaseAtomically_AndKeepsTenantsSeparate()
    {
        using var limiter = new RedisRateLimiter(Options.Create(new RateLimitOptions
        {
            Tenant = new RateLimitPolicy(2, 0),
            TelegramUser = new RateLimitPolicy(10, 0),
            Route = new RateLimitPolicy(10, 0),
        }), NullLogger<RedisRateLimiter>.Instance);

        var request = new RateLimitRequest(
            TenantId.Create("tenant-a"),
            PlayerId.Create("player-1"),
            BotChannel.Telegram,
            "command:coin-flip");

        var first = await limiter.CheckAsync(request);
        var second = await limiter.CheckAsync(request);
        var third = await limiter.CheckAsync(request);
        var otherTenant = await limiter.CheckAsync(request with { TenantId = TenantId.Create("tenant-b") });

        Assert.True(first.Allowed);
        Assert.True(second.Allowed);
        Assert.False(third.Allowed);
        Assert.Equal(RateLimitDimension.Tenant, third.DeniedDimension);
        Assert.True(third.IsFallback);
        Assert.True(otherTenant.Allowed);
    }

    [Fact]
    public void TelegramResolver_SeparatesTopics_AndPrivateContainers()
    {
        var resolver = new TelegramTenantContextResolver();
        var requestId = RequestId.Create("request-1");

        var topic = resolver.Resolve(
            new TelegramContainer("42", "7", "99"),
            requestId,
            requestId);
        var directMessage = resolver.Resolve(
            new TelegramContainer("7", "7", IsPrivateChat: true),
            requestId,
            requestId);

        Assert.Equal("telegram:chat:42", topic.TenantId.Value);
        Assert.Equal("topic:99", topic.ScopeId.Value);
        Assert.Equal("7", topic.PlayerId!.Value.Value);
        Assert.Equal("telegram:dm:7", directMessage.TenantId.Value);
        Assert.Equal("main", directMessage.ScopeId.Value);
    }

    [Fact]
    public async Task Limiter_UsesTenantPolicyProviderBeforeApplyingLease()
    {
        using var limiter = new RedisRateLimiter(
            Options.Create(new RateLimitOptions
            {
                Tenant = new RateLimitPolicy(100, 0),
                TelegramUser = new RateLimitPolicy(100, 0),
                Route = new RateLimitPolicy(100, 0),
            }),
            NullLogger<RedisRateLimiter>.Instance,
            new FixedPolicyProvider(new RateLimitPolicySet(
                new RateLimitPolicy(1, 0),
                new RateLimitPolicy(100, 0),
                new RateLimitPolicy(100, 0),
                new RateLimitPolicy(100, 0),
                new RateLimitPolicy(100, 0),
                "tenant-version-7")));

        var request = new RateLimitRequest(
            TenantId.Create("tenant-policy"),
            PlayerId.Create("player-1"),
            BotChannel.Telegram,
            "command:policy");

        var first = await limiter.CheckAsync(request);
        var second = await limiter.CheckAsync(request);

        Assert.True(first.Allowed);
        Assert.Equal("tenant-version-7", first.PolicyVersion);
        Assert.False(second.Allowed);
        Assert.Equal(RateLimitDimension.Tenant, second.DeniedDimension);
    }

    private sealed class FixedPolicyProvider(RateLimitPolicySet policies) : IRateLimitPolicyProvider
    {
        public ValueTask<RateLimitPolicySet> ResolveAsync(
            RateLimitRequest request,
            RateLimitPolicySet deployment,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(policies);
    }
}
