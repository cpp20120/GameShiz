using Discord.WebSocket;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using BotFramework.Discord.Hosting;
using BotFramework.Discord.Routing;
using BotFramework.Discord.Interactions;

namespace BotFramework.Discord.Composition;

public static class DiscordBuilderExtensions
{
    public static IHostApplicationBuilder AddDiscordBackend(this IHostApplicationBuilder builder)
    {
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
        builder.Services.AddScoped<DiscordMessageRouter>();
        builder.Services.AddScoped<DiscordInteractionRouter>();
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
        return app;
    }
}
