using BotFramework.Contracts.RateLimiting;
using BotFramework.Contracts.Tenancy;
using BotFramework.Host.Commands.RateLimiting;

namespace BotFramework.Host.Commands.Middleware;

/// <summary>
/// Applies the same distributed limiter used by the transports to commands
/// that enter through the command bus. Background/system commands without an
/// inbound tenant context are deliberately left untouched.
/// </summary>
public sealed class DistributedRateLimitMiddleware(
    IRateLimiter limiter,
    ITenantContextAccessor tenantContext,
    RateLimitRequestState requestState)
    : ICommandMiddleware
{
    public async Task InvokeAsync(CommandContext ctx, Func<Task> next)
    {
        if (requestState.LeaseGranted)
        {
            await next().ConfigureAwait(false);
            return;
        }

        var current = tenantContext.Current;
        if (current is null || current.PlayerId is not { } player)
        {
            await next().ConfigureAwait(false);
            return;
        }

        var decision = await limiter.CheckAsync(
            new RateLimitRequest(
                current.TenantId,
                player,
                current.Channel,
                $"command:{ctx.Command.ModuleId}:{ctx.Command.GetType().Name}"),
            ctx.Cancellation).ConfigureAwait(false);
        ctx.Items["rate_limit_decision"] = decision;

        if (!decision.Allowed)
            throw new RateLimitedException(new RateLimitKey(0, ctx.Command.GetType().Name));

        requestState.LeaseGranted = true;
        await next().ConfigureAwait(false);
    }
}
