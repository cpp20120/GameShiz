using Discord.WebSocket;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using BotFramework.Discord.Hosting;
using BotFramework.Discord.Routing;
using BotFramework.Discord.Interactions;
using BotFramework.Host.Contracts.Discord;
using BotFramework.Host.DiscordOutbox;
using BotFramework.Host.Persistence.Connections;
using BotFramework.Contracts.Tenancy;
using BotFramework.Discord.Abstractions;
using BotFramework.Contracts.RateLimiting;
using BotFramework.Host.RateLimiting;
using BotFramework.Host.Tenancy;

namespace BotFramework.Discord.Composition;

public static class DiscordBuilderExtensions
{
    public static IHostApplicationBuilder AddDiscordBackend(this IHostApplicationBuilder builder)
    {
        builder.Configuration["Transport:Channel"] = "discord";
        builder.Services.AddOptions<DiscordOptions>()
            .Bind(builder.Configuration.GetSection(DiscordOptions.SectionName))
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Token),
                "Discord:Token is required when Discord:Enabled is true.")
            .Validate(options => !options.Enabled || !string.IsNullOrEmpty(options.CommandPrefix),
                "Discord:CommandPrefix is required when Discord:Enabled is true.")
            .ValidateOnStart();

        builder.Services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DiscordOptions>>().Value;
            return new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = options.GatewayIntents,
                AlwaysDownloadUsers = false,
            });
        });
        builder.Services.AddSingleton<INpgsqlConnectionFactory, NpgsqlConnectionFactory>();
        builder.Services.AddSingleton<ITenantContextProvisioner, PostgresTenantContextProvisioner>();
        builder.Services.AddHealthChecks()
            .AddCheck<BotFramework.Host.Composition.ServiceDatabases.PostgresDatabaseHealthCheck>(
                "postgres",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                tags: ["ready"]);
        builder.Services.AddSingleton<PostgresDiscordOutboxStore>();
        builder.Services.AddSingleton<IDiscordOutboxStore>(sp => sp.GetRequiredService<PostgresDiscordOutboxStore>());
        builder.Services.AddSingleton<IDiscordOutbox>(sp => sp.GetRequiredService<PostgresDiscordOutboxStore>());
        builder.Services.AddHostedService<DiscordOutboxDispatcherService>();
        builder.Services.AddOptions<RateLimitOptions>()
            .Bind(builder.Configuration.GetSection(RateLimitOptions.SectionName))
            .Configure(options => options.RedisConnectionString ??= builder.Configuration["Redis:ConnectionString"])
            .Validate(options => options.LocalMaxKeys > 0, "RateLimit:LocalMaxKeys must be positive.")
            .ValidateOnStart();
        builder.Services.AddSingleton<IRateLimiter, BotFramework.Host.RateLimiting.RedisRateLimiter>();
        builder.Services.AddSingleton<BotFramework.Host.RateLimiting.PostgresRateLimitPolicyProvider>();
        builder.Services.AddSingleton<IRateLimitPolicyProvider>(sp =>
            sp.GetRequiredService<BotFramework.Host.RateLimiting.PostgresRateLimitPolicyProvider>());
        builder.Services.AddSingleton<IRateLimitPolicyAdmin>(sp =>
            sp.GetRequiredService<BotFramework.Host.RateLimiting.PostgresRateLimitPolicyProvider>());
        builder.Services.AddSingleton<IDiscordTenantContextResolver, DiscordTenantContextResolver>();
        builder.Services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();
        builder.Services.AddScoped<RateLimitRequestState>();
        builder.Services.AddScoped<DiscordMessageRouter>();
        builder.Services.AddScoped<DiscordInteractionRouter>();
        builder.Services.AddSingleton<IDiscordComponentTokenStore, DiscordComponentTokenStore>();
        builder.Services.AddScoped<IDiscordInteractionHandler, DiscordCasinoMenuHandler>();
        builder.Services.AddScoped<IDiscordMessageHandler, BotFramework.Discord.Commands.DiscordHelpHandler>();
        builder.Services.AddHostedService<DiscordHostedService>();
        return builder;
    }

    public static WebApplication UseDiscordBackend(this WebApplication app)
    {
        app.MapGet("/health/live", () => Results.Ok(new
        {
            status = "healthy",
            service = "casinoshiz-discord-bff",
        }));
        app.MapHealthChecks("/health/ready");
        return app;
    }
}
