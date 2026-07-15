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
        builder.Services.AddHealthChecks()
            .AddCheck<BotFramework.Host.Composition.ServiceDatabases.PostgresDatabaseHealthCheck>(
                "postgres",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                tags: ["ready"]);
        builder.Services.AddSingleton<PostgresDiscordOutboxStore>();
        builder.Services.AddSingleton<IDiscordOutboxStore>(sp => sp.GetRequiredService<PostgresDiscordOutboxStore>());
        builder.Services.AddSingleton<IDiscordOutbox>(sp => sp.GetRequiredService<PostgresDiscordOutboxStore>());
        builder.Services.AddHostedService<DiscordOutboxDispatcherService>();
        builder.Services.AddScoped<DiscordMessageRouter>();
        builder.Services.AddScoped<DiscordInteractionRouter>();
        builder.Services.AddSingleton<IDiscordComponentTokenStore, DiscordComponentTokenStore>();
        builder.Services.AddSingleton<DiscordUxRateLimiter>();
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
