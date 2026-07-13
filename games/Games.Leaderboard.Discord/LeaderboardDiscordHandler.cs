using BotFramework.Discord.Commands;
using BotFramework.Discord.Interactions;
using BotFramework.Discord.Routing;
using Discord;
using Discord.WebSocket;
using Games.Leaderboard.Contracts;

namespace Games.Leaderboard.Discord;

public sealed class LeaderboardDiscordHandler(ILeaderboardClient client) : IDiscordMessageHandler
{
    public bool CanHandle(DiscordMessageContext c) => DiscordCommand.Is(c, "balance", "top", "daily", "globaltop");
    public async Task HandleAsync(DiscordMessageContext c)
    {
        var p = DiscordCommand.Parts(c);
        var cmd = p[0].ToLowerInvariant();
        var uid = DiscordCommand.UserId(c);
        var scope = DiscordCommand.ScopeId(c);
        var name = DiscordCommand.DisplayName(c);
        object r = cmd switch
        {
            "balance" => await client.GetBalanceAsync(uid, scope, name, c.CancellationToken),
            "daily" => await client.ClaimDailyAsync(uid, scope, name, c.CancellationToken),
            "globaltop" => await client.GetGlobalTopAsync(10, c.CancellationToken),
            _ => await client.GetTopAsync(10, scope, c.CancellationToken),
        };
        await DiscordCommand.ReplyResultAsync(c, r, "Leaderboard");
    }
}

public sealed class LeaderboardDiscordInteractionHandler(ILeaderboardClient client) : IDiscordInteractionHandler
{
    public IEnumerable<ApplicationCommandProperties> BuildCommands()
    {
        yield return new SlashCommandBuilder()
            .WithName("profile")
            .WithDescription("Баланс, бонусы и рейтинги")
            .AddOption(Subcommand("balance", "Показать баланс"))
            .AddOption(Subcommand("daily", "Получить ежедневный бонус"))
            .AddOption(Subcommand("top", "Рейтинг текущего сервера"))
            .AddOption(Subcommand("global-top", "Глобальный рейтинг"))
            .Build();
    }

    public bool CanHandle(SocketInteraction interaction) => interaction is SocketSlashCommand command && command.Data.Name == "profile";

    public async Task HandleAsync(DiscordInteractionContext context)
    {
        var command = (SocketSlashCommand)context.Interaction;
        var action = DiscordInteraction.Subcommand(command)?.Name ?? "balance";
        var userId = DiscordInteraction.UserId(context);
        var scopeId = DiscordInteraction.ScopeId(context);
        object result = action switch
        {
            "daily" => await client.ClaimDailyAsync(userId, scopeId, DiscordInteraction.DisplayName(context), context.CancellationToken),
            "top" => await client.GetTopAsync(10, scopeId, context.CancellationToken),
            "global-top" => await client.GetGlobalTopAsync(10, context.CancellationToken),
            _ => await client.GetBalanceAsync(userId, scopeId, DiscordInteraction.DisplayName(context), context.CancellationToken),
        };
        await DiscordInteraction.ReplyResultAsync(context, result, "Profile", ephemeral: true);
    }

    private static SlashCommandOptionBuilder Subcommand(string name, string description) => new SlashCommandOptionBuilder()
        .WithName(name).WithDescription(description).WithType(ApplicationCommandOptionType.SubCommand);
}

public static class LeaderboardDiscordModule
{
    public static IServiceCollection AddLeaderboardDiscord(this IServiceCollection services) => services
        .AddScoped<IDiscordMessageHandler, LeaderboardDiscordHandler>()
        .AddScoped<IDiscordInteractionHandler, LeaderboardDiscordInteractionHandler>();
}
