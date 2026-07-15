using BotFramework.Host.Localization;
using BotFramework.Host.Pipeline.Middleware;
using BotFramework.Host.Pipeline.Routing;
using BotFramework.Host.Runtime.Hosting;
using BotFramework.Host.Redis.Streams;
using BotFramework.Host.TelegramOutbox;
using BotFramework.Telegram.Outbox;
using DotNetCore.CAP;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;
using BotFramework.Rendering;
using BotFramework.Contracts.Tenancy;
using BotFramework.Telegram.Abstractions.Tenancy;
using BotFramework.Contracts.RateLimiting;
using BotFramework.Host.RateLimiting;

namespace BotFramework.Host.Composition.Builder;

public static class TelegramBffBuilderExtensions
{
    public static IBotFrameworkBuilder AddTelegramBff(this IHostApplicationBuilder builder)
    {
        builder.Configuration["Transport:Channel"] = "telegram";
        var services = builder.Services;
        var configuration = builder.Configuration;
        services.AddBotFrameworkRendering(configuration);
        services.Configure<BotFrameworkOptions>(
            builder.Configuration.GetSection(BotFrameworkOptions.SectionName));
        services.AddSingleton<ITelegramBotClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<BotFrameworkOptions>>().Value;
            if (string.IsNullOrWhiteSpace(options.Token))
                throw new InvalidOperationException("Set Bot:Token for the Telegram BFF.");
            return new TelegramBotClient(options.Token);
        });
        services.AddSingleton<UpdateRouter>();
        services.AddScoped<UpdatePipeline>();
        services.AddSingleton<ITelegramTenantContextResolver, TelegramTenantContextResolver>();
        services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();
        services.AddScoped<RateLimitRequestState>();
        services.AddSingleton<IUpdateMiddleware, LoggingMiddleware>();
        services.AddScoped<IUpdateMiddleware, RateLimitMiddleware>();
        services.AddOptions<RateLimitOptions>()
            .Bind(builder.Configuration.GetSection(RateLimitOptions.SectionName))
            .Configure(options => options.RedisConnectionString ??= builder.Configuration["Redis:ConnectionString"])
            .Validate(options => options.LocalMaxKeys > 0, "RateLimit:LocalMaxKeys must be positive.")
            .ValidateOnStart();
        services.AddSingleton<IRateLimiter, BotFramework.Host.RateLimiting.RedisRateLimiter>();
        services.TryAddSingleton<IRateLimitPolicyProvider, DefaultRateLimitPolicyProvider>();
        services.AddSingleton<ILocalizer, Localizer>();
        services.AddHostedService<BotHostedService>();
        var useCapOutboxTransport = string.Equals(
            configuration[$"{TelegramOutboxTransportOptions.SectionName}:Transport"],
            "Cap",
            StringComparison.OrdinalIgnoreCase);
        if (useCapOutboxTransport)
        {
            var postgres = configuration.GetConnectionString("Postgres")
                ?? throw new InvalidOperationException("TelegramOutbox:Transport=Cap requires ConnectionStrings:Postgres.");
            var redis = configuration[$"{RedisOptions.SectionName}:ConnectionString"];
            if (string.IsNullOrWhiteSpace(redis))
                throw new InvalidOperationException("TelegramOutbox:Transport=Cap requires Redis:ConnectionString.");

            services.AddCap(options =>
            {
                options.UsePostgreSql(postgres);
                options.UseRedis(redis);
                options.DefaultGroupName = "casinoshiz.telegram-bff";
            });
            services.AddSingleton<TelegramOutboxCapDeliveryConsumer>();
        }
        return new BotFrameworkBuilder(services, builder.Configuration);
    }

    public static WebApplication UseTelegramBff(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<BotFrameworkOptions>>().Value;
        if (options.IsProduction && !string.IsNullOrWhiteSpace(options.Token))
        {
            app.MapPost($"/{options.Token}", async (HttpContext httpContext) =>
            {
                var update = await httpContext.Request.ReadFromJsonAsync<Update>(httpContext.RequestAborted);
                if (update is null) return Results.BadRequest();

                using var scope = httpContext.RequestServices.CreateScope();
                var pipeline = scope.ServiceProvider.GetRequiredService<UpdatePipeline>();
                var bot = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
                await pipeline.InvokeAsync(new UpdateContext(
                    bot,
                    update,
                    scope.ServiceProvider,
                    httpContext.RequestAborted));
                return Results.Ok();
            });
        }

        app.MapGet("/health/live", () => Results.Ok(new
        {
            status = "healthy",
            service = "casinoshiz-telegram-bff",
        }));
        return app;
    }
}
