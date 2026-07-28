using BotFramework.Contracts.RateLimiting;
using BotFramework.Rest.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace BotFramework.Rest;

internal sealed class RestRateLimitMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IRateLimiter limiter, RateLimitRequestState requestState)
    {
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var request = context.GetRestRequestContext();
        var routeKey = context.GetEndpoint()?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName
            ?? context.GetEndpoint()?.DisplayName
            ?? "rest.unknown";
        var decision = await limiter.CheckAsync(
            new RateLimitRequest(
                request.Tenant,
                request.Player,
                BotFramework.Contracts.Messaging.BotChannel.Rest,
                routeKey,
                context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);

        context.Response.Headers["RateLimit-Limit"] = decision.Limit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        context.Response.Headers["RateLimit-Remaining"] = decision.Remaining.ToString(System.Globalization.CultureInfo.InvariantCulture);
        context.Response.Headers["RateLimit-Policy-Version"] = decision.PolicyVersion;
        if (decision.IsFallback)
            context.Response.Headers["RateLimit-Fallback"] = "local";

        if (!decision.Allowed)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(decision.RetryAfter.TotalSeconds))
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
            await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Rate limit exceeded.",
                    Detail = "The request quota for this tenant or route has been exceeded.",
                    Type = "https://httpstatuses.com/429",
                    Extensions =
                    {
                        ["code"] = "rate_limit_exceeded",
                        ["retryAfterSeconds"] = Math.Max(1, (int)Math.Ceiling(decision.RetryAfter.TotalSeconds)),
                        ["limiterDimension"] = decision.DeniedDimension?.ToString(),
                    },
                },
            });
            return;
        }

        requestState.LeaseGranted = true;
        await next(context);
    }
}
