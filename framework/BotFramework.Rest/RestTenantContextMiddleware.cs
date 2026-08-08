using System.Diagnostics;
using System.Diagnostics.Metrics;
using BotFramework.Contracts.Observability;
using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace BotFramework.Rest;

internal sealed class RestTenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContextAccessor accessor)
    {
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var request = context.GetRestRequestContext();
        context.Response.Headers["X-Request-ID"] = request.RequestIdentifier.ToString();
        context.Response.Headers["X-Correlation-ID"] = request.CorrelationIdentifier.ToString();

        var provisioner = context.RequestServices.GetService<ITenantContextProvisioner>();
        if (provisioner is not null)
            await provisioner.EnsureAsync(request.TenantContext, context.RequestAborted);

        using var metadataScope = RequestMetadataContext.Push(
            RequestMetadata.FromTenantContext(request.TenantContext, "rest"));
        using var tenantScope = accessor.Push(request.TenantContext);
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "success";
        var route = context.GetEndpoint()?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName
            ?? context.GetEndpoint()?.DisplayName
            ?? "rest.unknown";
        var requestTags = new TagList
        {
            { "service", "rest" },
            { "channel", "rest" },
            { "route", route },
        };
        BotFrameworkMetrics.Requests.Add(1, requestTags);
        try
        {
            await next(context);
        }
        catch
        {
            outcome = "error";
            var errorTags = requestTags;
            errorTags.Add("outcome", outcome);
            BotFrameworkMetrics.RequestErrors.Add(1, errorTags);
            throw;
        }
        finally
        {
            var durationTags = requestTags;
            durationTags.Add("outcome", outcome);
            BotFrameworkMetrics.RequestDuration.Record(
                Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
                durationTags);
        }
    }
}
