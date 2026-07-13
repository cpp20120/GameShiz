using BotFramework.Discord.Interactions;
using BotFramework.Discord.Routing;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BotFramework.Discord.Hosting;

public sealed partial class DiscordHostedService(
    DiscordSocketClient client,
    IServiceScopeFactory scopeFactory,
    IOptions<DiscordOptions> options,
    IHostApplicationLifetime lifetime,
    ILogger<DiscordHostedService> logger) : BackgroundService
{
    private readonly DiscordOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            LogDisabled(logger);
            return;
        }

        client.Log += OnDiscordLogAsync;
        client.Ready += OnReadyAsync;
        client.MessageReceived += OnMessageReceivedAsync;
        client.SlashCommandExecuted += OnInteractionAsync;
        client.ButtonExecuted += OnInteractionAsync;
        client.SelectMenuExecuted += OnInteractionAsync;

        await client.LoginAsync(TokenType.Bot, _options.Token);
        await client.StartAsync();
        LogStarted(logger, client.CurrentUser?.Id ?? 0);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            client.SelectMenuExecuted -= OnInteractionAsync;
            client.ButtonExecuted -= OnInteractionAsync;
            client.SlashCommandExecuted -= OnInteractionAsync;
            client.MessageReceived -= OnMessageReceivedAsync;
            client.Ready -= OnReadyAsync;
            client.Log -= OnDiscordLogAsync;
            await client.StopAsync();
            await client.LogoutAsync();
        }
    }

    private async Task OnReadyAsync()
    {
        if (!_options.RegisterApplicationCommands) return;

        using var scope = scopeFactory.CreateScope();
        var commands = scope.ServiceProvider.GetServices<IDiscordInteractionHandler>()
            .SelectMany(x => x.BuildCommands())
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();

        if (commands.Length == 0) return;

        if (_options.ApplicationCommandsGuildId is { } guildId)
        {
            var guild = client.GetGuild(guildId)
                ?? throw new InvalidOperationException($"Discord guild {guildId} is unavailable for command registration.");
            await guild.BulkOverwriteApplicationCommandAsync(commands);
            LogCommandsRegistered(logger, commands.Length, guildId);
            return;
        }

        await client.BulkOverwriteGlobalApplicationCommandsAsync(commands);
        LogCommandsRegistered(logger, commands.Length, null);
    }

    private async Task OnMessageReceivedAsync(SocketMessage message)
    {
        if (message.Author.IsBot || string.IsNullOrWhiteSpace(message.Content)) return;
        if (!TryGetCommand(message.Content, _options.CommandPrefix, out var commandText)) return;

        using var scope = scopeFactory.CreateScope();
        var router = scope.ServiceProvider.GetRequiredService<DiscordMessageRouter>();
        var context = new DiscordMessageContext(
            message,
            commandText,
            scope.ServiceProvider,
            lifetime.ApplicationStopping);
        await router.RouteAsync(context);
    }

    private async Task OnInteractionAsync(SocketInteraction interaction)
    {
        using var scope = scopeFactory.CreateScope();
        var router = scope.ServiceProvider.GetRequiredService<DiscordInteractionRouter>();
        await router.RouteAsync(new DiscordInteractionContext(
            interaction,
            scope.ServiceProvider,
            lifetime.ApplicationStopping));
    }

    private Task OnDiscordLogAsync(LogMessage message)
    {
        logger.Log(Map(message.Severity), message.Exception, "Discord: {Message}", message.Message);
        return Task.CompletedTask;
    }

    private static bool TryGetCommand(string content, string prefix, out string commandText)
    {
        commandText = string.Empty;
        if (string.IsNullOrEmpty(prefix) || !content.StartsWith(prefix, StringComparison.Ordinal)) return false;
        commandText = content[prefix.Length..].Trim();
        return commandText.Length > 0;
    }

    private static LogLevel Map(LogSeverity severity) => severity switch
    {
        LogSeverity.Critical => LogLevel.Critical,
        LogSeverity.Error => LogLevel.Error,
        LogSeverity.Warning => LogLevel.Warning,
        LogSeverity.Info => LogLevel.Information,
        LogSeverity.Verbose => LogLevel.Trace,
        LogSeverity.Debug => LogLevel.Debug,
        _ => LogLevel.Information,
    };

    [LoggerMessage(LogLevel.Information, "Discord backend is disabled")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(LogLevel.Information, "Discord backend started as bot user {BotUserId}")]
    private static partial void LogStarted(ILogger logger, ulong botUserId);

    [LoggerMessage(LogLevel.Information, "Registered {CommandCount} Discord application commands (guild {GuildId})")]
    private static partial void LogCommandsRegistered(ILogger logger, int commandCount, ulong? guildId);
}
