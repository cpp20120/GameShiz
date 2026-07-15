using System.Globalization;
using System.Collections.Concurrent;
using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.RateLimiting;
using BotFramework.Contracts.Tenancy;
using BotFramework.Telegram.Abstractions.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace BotFramework.Host.Pipeline.Middleware;

public sealed partial class RateLimitMiddleware(
    IRateLimiter limiter,
    ITelegramTenantContextResolver resolver,
    ITenantContextAccessor tenantContext,
    RateLimitRequestState requestState,
    ILogger<RateLimitMiddleware> logger) : IUpdateMiddleware
{
    // Kept for the framework's direct middleware tests and source consumers
    // that instantiate the middleware without a DI container. Production
    // composition always uses the primary constructor and the distributed
    // limiter registered by the BFF/Host.
    public RateLimitMiddleware(ILogger<RateLimitMiddleware> logger)
        : this(
            new CompatibilityLimiter(),
            new TelegramTenantContextResolver(),
            new TenantContextAccessor(),
            new RateLimitRequestState(),
            logger)
    {
    }

    public async Task InvokeAsync(UpdateContext ctx, UpdateDelegate next)
    {
        var userId = ctx.UserId;
        if (userId == 0)
        {
            await next(ctx);
            return;
        }

        var requestId = RequestId.Create(string.Create(
            CultureInfo.InvariantCulture,
            $"telegram:update:{ctx.Update.Id}"));
        var tenant = resolver.Resolve(
            new TelegramContainer(
                ctx.ChatId.ToString(CultureInfo.InvariantCulture),
                userId.ToString(CultureInfo.InvariantCulture),
                TopicId(ctx),
                IsPrivateChat(ctx)),
            requestId,
            requestId);
        using var tenantScope = tenantContext.Push(tenant);

        // Direct middleware consumers/tests may intentionally provide no scoped
        // service provider. Tenant provisioning is an optional host concern;
        // rate limiting must still work in that composition mode.
        var provisioner = ctx.Services?.GetService<ITenantContextProvisioner>();
        if (provisioner is not null)
            await provisioner.EnsureAsync(tenant, ctx.Ct).ConfigureAwait(false);

        using var metadataScope = RequestMetadataContext.Push(
            RequestMetadata.FromTenantContext(tenant, "telegram"));
        var decision = await limiter.CheckAsync(
            new RateLimitRequest(
                tenant.TenantId,
                tenant.PlayerId,
                BotFramework.Contracts.Messaging.BotChannel.Telegram,
                RouteKey(ctx)),
            ctx.Ct).ConfigureAwait(false);
        ctx.Items["tenant_context"] = tenant;
        ctx.Items["rate_limit_decision"] = decision;
        if (!decision.Allowed)
        {
            LogRateLimited(userId, decision.DeniedDimension?.ToString() ?? "unknown");
            return;
        }

        requestState.LeaseGranted = true;
        await next(ctx);
    }

    private static string RouteKey(UpdateContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.Text) && context.Text!.TrimStart().StartsWith('/'))
        {
            var command = context.Text!.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            command = command[1..].Split('@', 2)[0];
            if (command.Length > 0 && command.All(c => char.IsLetterOrDigit(c) || c is '_' or ':' or '-'))
                return $"command:{command.ToLowerInvariant()}";
        }

        return context.Update switch
        {
            { CallbackQuery: not null } => "callback",
            { Message: { Dice: not null } } => "dice",
            { InlineQuery: not null } => "inline",
            { ChannelPost: not null } => "channel-post",
            _ => "update",
        };
    }

    private static string? TopicId(UpdateContext context) =>
        (context.MessageOrEdited ?? context.Update.CallbackQuery?.Message)
            ?.MessageThreadId?.ToString(CultureInfo.InvariantCulture);

    private static bool IsPrivateChat(UpdateContext context) =>
        (context.MessageOrEdited ?? context.Update.CallbackQuery?.Message)
            ?.Chat.Type == global::Telegram.Bot.Types.Enums.ChatType.Private;

    [LoggerMessage(EventId = 1200, Level = LogLevel.Warning, Message = "ratelimit.drop user={UserId} dimension={Dimension}")]
    partial void LogRateLimited(long userId, string dimension);

    private sealed class CompatibilityLimiter : IRateLimiter
    {
        private static readonly ConcurrentDictionary<string, Bucket> Buckets = new(StringComparer.Ordinal);

        public ValueTask<RateLimitDecision> CheckAsync(
            RateLimitRequest request,
            CancellationToken cancellationToken = default)
        {
            var key = $"{request.TenantId.Value}:{request.PlayerId?.Value}:{request.RouteKey}";
            var now = DateTimeOffset.UtcNow;
            var bucket = Buckets.GetOrAdd(key, _ => new Bucket(now));
            lock (bucket)
            {
                bucket.Tokens = Math.Min(10, bucket.Tokens + (now - bucket.UpdatedAt).TotalSeconds);
                bucket.UpdatedAt = now;
                if (bucket.Tokens < 1)
                    return ValueTask.FromResult(new RateLimitDecision(
                        false,
                        RateLimitDimension.TenantPlayer,
                        10,
                        0,
                        TimeSpan.FromSeconds(1 - bucket.Tokens),
                        false,
                        "compatibility-test"));

                bucket.Tokens -= 1;
                return ValueTask.FromResult(new RateLimitDecision(
                    true,
                    null,
                    10,
                    (int)Math.Floor(bucket.Tokens),
                    TimeSpan.Zero,
                    false,
                    "compatibility-test"));
            }
        }

        private sealed class Bucket(DateTimeOffset updatedAt)
        {
            public double Tokens { get; set; } = 10;
            public DateTimeOffset UpdatedAt { get; set; } = updatedAt;
        }
    }
}
