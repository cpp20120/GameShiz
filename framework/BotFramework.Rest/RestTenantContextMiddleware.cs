using System.Diagnostics;
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
        BotFrameworkMetrics.Requests.Add(
            1,
            new KeyValuePair<string, object?>("service", "rest"),
            new KeyValuePair<string, object?>("channel", "rest"),
            new KeyValuePair<string, object?>("route", route));
        try
        {
            await next(context);
        }
        catch
        {
            outcome = "error";
            BotFrameworkMetrics.RequestErrors.Add(
                1,
                new KeyValuePair<string, object?>("service", "rest"),
                new KeyValuePair<string, object?>("channel", "rest"),
                new KeyValuePair<string, object?>("route", route),
                new KeyValuePair<string, object?>("outcome", outcome));
            throw;
        }
        finally
        {
            BotFrameworkMetrics.RequestDuration.Record(
                Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
                new KeyValuePair<string, object?>("service", "rest"),
                new KeyValuePair<string, object?>("channel", "rest"),
                new KeyValuePair<string, object?>("route", route),
                new KeyValuePair<string, object?>("outcome", outcome));
        }
    }
}
