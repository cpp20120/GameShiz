using BotFramework.Host.Composition.Builder;
using BotFramework.Host.Configuration;
using BotFramework.Host.Pipeline.Middleware;
using BotFramework.Host.Pipeline.Routing;
using BotFramework.Host.Redis.Streams;
using BotFramework.Host.Runtime.Hosting;
using BotFramework.Host.TelegramOutbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using BotFramework.Sdk.Pipeline;
using StackExchange.Redis;
using Telegram.Bot;
using BotFramework.Contracts.Tenancy;
using BotFramework.Contracts.RateLimiting;
using BotFramework.Telegram.Abstractions.Tenancy;

namespace BotFramework.Telegram.Composition;

public static class LegacyBotFrameworkBuilderExtensions
{
    public static IBotFrameworkBuilder AddBotFramework(this IHostApplicationBuilder builder)
    {
        var result = builder.AddBackendFramework();
        var services = builder.Services;
        var configuration = builder.Configuration;

        services.AddSingleton<ITelegramBotClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<BotFrameworkOptions>>().Value;
            return string.IsNullOrWhiteSpace(options.Token) ? throw new InvalidOperationException("Set Bot:Token in configuration.") : new TelegramBotClient(options.Token);
        });
        services.AddSingleton<UpdateRouter>();
        services.AddScoped<UpdatePipeline>();
        services.AddSingleton<ITelegramTenantContextResolver>(_ =>
            new TelegramTenantContextResolver(configuration["Bot:TenantKey"]));
        services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();
        services.AddScoped<RateLimitRequestState>();
        services.AddSingleton<IUpdateMiddleware, UpdateAnalyticsMiddleware>();
        services.AddSingleton<IUpdateMiddleware, ExceptionMiddleware>();
        services.AddSingleton<IUpdateMiddleware, UpdateDeduplicationMiddleware>();
        services.AddSingleton<IUpdateMiddleware, LoggingMiddleware>();
        services.AddScoped<IUpdateMiddleware, RateLimitMiddleware>();
        services.AddSingleton<IUpdateMiddleware, KnownChatsMiddleware>();
        var useCapOutboxTransport = string.Equals(
            configuration[$"{TelegramOutboxTransportOptions.SectionName}:Transport"],
            "Cap",
            StringComparison.OrdinalIgnoreCase);
        if (!useCapOutboxTransport)
        {
            services.AddSingleton<TelegramOutboxDispatcherService>();
            services.AddHostedService(sp => sp.GetRequiredService<TelegramOutboxDispatcherService>());
        }
        services.AddHostedService<BotHostedService>();

        var redisEnabled = configuration.GetValue<bool>($"{RedisOptions.SectionName}:Enabled");
        if (!redisEnabled) return result;
        services.AddSingleton<UpdateStreamPublisher>();
        services.AddHostedService<UpdateStreamWorkerService>();

        return result;
    }
}
