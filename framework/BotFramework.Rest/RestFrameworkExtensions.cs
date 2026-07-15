using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

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

        services.AddHttpContextAccessor();
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
                options.MetadataAddress = jwt.MetadataAddress;
                options.Audience = jwt.Audience;
                options.RequireHttpsMetadata = jwt.RequireHttpsMetadata;
                options.TokenValidationParameters.ValidIssuer = jwt.Issuer;
                options.TokenValidationParameters.ValidateIssuer = !string.IsNullOrWhiteSpace(jwt.Issuer)
                    || !string.IsNullOrWhiteSpace(jwt.Authority);
                options.TokenValidationParameters.ValidateAudience = !string.IsNullOrWhiteSpace(jwt.Audience);
            });
        services.AddAuthorization();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = static (context, _) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "1";
                return ValueTask.CompletedTask;
            };
            options.AddPolicy(PolicyName, httpContext =>
            {
                var subject = httpContext.User.FindFirst("sub")?.Value;
                var partition = string.IsNullOrWhiteSpace(subject)
                    ? $"ip:{httpContext.Connection.RemoteIpAddress}"
                    : $"user:{subject}";
                var settings = httpContext.RequestServices.GetRequiredService<IOptions<RestFrameworkOptions>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.PermitLimit,
                    Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                });
            });
        });

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
        app.UseRateLimiter();
        return app;
    }

    public static RouteGroupBuilder MapRestGroup(this IEndpointRouteBuilder endpoints, string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<RestFrameworkOptions>>().Value;
        return endpoints.MapGroup($"/api/{options.ApiVersion}/scopes/{{scopeId}}/{moduleId}")
            .RequireAuthorization()
            .RequireRateLimiting(PolicyName)
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
