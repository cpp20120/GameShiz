using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using BotFramework.Contracts.RateLimiting;
using BotFramework.Contracts.Observability;
using BotFramework.Rest.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;
using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;

namespace BotFramework.Rest;

public static class RestFrameworkExtensions
{
    public const string PolicyName = "rest-api";

    public static IHostApplicationBuilder AddRestFramework(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddOptions<RestFrameworkOptions>()
            .Bind(builder.Configuration.GetSection(RestFrameworkOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiVersion), "Rest:ApiVersion is required.")
            .Validate(options => options.PermitLimit > 0 && options.WindowSeconds > 0, "REST rate-limit values must be positive.")
            .ValidateOnStart();
        services.AddOptions<RateLimitOptions>()
            .Bind(builder.Configuration.GetSection(RateLimitOptions.SectionName))
            .Configure(options => options.RedisConnectionString ??= builder.Configuration["Redis:ConnectionString"])
            .Validate(options => options.LocalMaxKeys > 0, "RateLimit:LocalMaxKeys must be positive.")
            .ValidateOnStart();
        services.AddSingleton<IRateLimiter, RedisRateLimiter>();
        services.TryAddSingleton<IRateLimitPolicyProvider, DefaultRateLimitPolicyProvider>();
        services.AddScoped<RateLimitRequestState>();

        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddScoped<RestRequestContext>(sp =>
            sp.GetRequiredService<RestRequestContextFactory>().Create(
                sp.GetRequiredService<IHttpContextAccessor>().HttpContext
                    ?? throw new InvalidOperationException("REST request context is only available during an HTTP request.")));
        services.AddSingleton<RestRequestContextFactory>();

        services.AddHealthChecks();
        services.AddProblemDetails(problemDetails =>
        {
            problemDetails.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["correlationId"] =
                    context.HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                    ?? context.HttpContext.TraceIdentifier;
            };
        });
        services.AddExceptionHandler<RestExceptionHandler>();

        var jwt = builder.Configuration.GetSection($"{RestFrameworkOptions.SectionName}:Jwt").Get<RestFrameworkOptions.JwtOptions>()
            ?? new RestFrameworkOptions.JwtOptions();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = jwt.Authority;
                if (!string.IsNullOrWhiteSpace(jwt.MetadataAddress))
                    options.MetadataAddress = jwt.MetadataAddress;
                options.Audience = jwt.Audience;
                options.RequireHttpsMetadata = jwt.RequireHttpsMetadata;
                options.TokenValidationParameters.ValidIssuer = jwt.Issuer;
                options.TokenValidationParameters.ValidateIssuer = !string.IsNullOrWhiteSpace(jwt.Issuer)
                    || !string.IsNullOrWhiteSpace(jwt.Authority);
                options.TokenValidationParameters.ValidateAudience = !string.IsNullOrWhiteSpace(jwt.Audience);
            });
        services.AddAuthorization();

        services.AddOpenApi("v1");
        return builder;
    }

    public static IApplicationBuilder UseRestFramework(this IApplicationBuilder app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages(async statusCodeContext =>
        {
            var response = statusCodeContext.HttpContext.Response;
            if (response.HasStarted || response.ContentLength is not null || response.StatusCode < 400)
                return;
            await statusCodeContext.HttpContext.RequestServices
                .GetRequiredService<IProblemDetailsService>()
                .WriteAsync(new ProblemDetailsContext { HttpContext = statusCodeContext.HttpContext })
                .ConfigureAwait(false);
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<RestTenantContextMiddleware>();
        app.UseMiddleware<RestRateLimitMiddleware>();
        return app;
    }

    public static RouteGroupBuilder MapRestGroup(this IEndpointRouteBuilder endpoints, string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<RestFrameworkOptions>>().Value;
        return endpoints.MapGroup($"/api/{options.ApiVersion}/tenants/{{tenantId}}/scopes/{{scopeId}}/{moduleId}")
            .RequireAuthorization()
            .AddEndpointFilter<RestRequestContextEndpointFilter>()
            .WithTags(moduleId);
    }

    public static IEndpointRouteBuilder MapRestFramework(this IEndpointRouteBuilder endpoints)
    {
        foreach (var module in endpoints.ServiceProvider.GetServices<IRestRouteModule>())
            module.Map(endpoints);

        endpoints.MapHealthChecks("/health/live", new() { Predicate = _ => false });
        endpoints.MapHealthChecks("/health/ready");

        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<RestFrameworkOptions>>().Value;
        var environment = endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (options.OpenApiEnabled && (environment.IsDevelopment() || options.ExposeOpenApiOutsideDevelopment))
            endpoints.MapOpenApi("/openapi/{documentName}.json");

        return endpoints;
    }
}

internal sealed class RestTenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContextAccessor accessor)
    {
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var request = context.GetRestRequestContext();
        context.Response.Headers["X-Request-ID"] = request.RequestIdentifier.ToString();
        context.Response.Headers["X-Correlation-ID"] = request.CorrelationIdentifier.ToString();

        var provisioner = context.RequestServices.GetService<ITenantContextProvisioner>();
        if (provisioner is not null)
            await provisioner.EnsureAsync(request.TenantContext, context.RequestAborted).ConfigureAwait(false);

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
            await next(context).ConfigureAwait(false);
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

internal sealed class RestRateLimitMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IRateLimiter limiter, RateLimitRequestState requestState)
    {
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
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
            context.RequestAborted).ConfigureAwait(false);

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
                ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
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
            }).ConfigureAwait(false);
            return;
        }

        requestState.LeaseGranted = true;
        await next(context).ConfigureAwait(false);
    }
}
