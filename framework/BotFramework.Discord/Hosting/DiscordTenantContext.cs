using System.Globalization;
using BotFramework.Contracts.Tenancy;
using BotFramework.Discord.Abstractions;
using Discord.WebSocket;

namespace BotFramework.Discord.Hosting;

internal static class DiscordTenantContext
{
    public static TenantContext Resolve(
        SocketMessage message,
        IDiscordTenantContextResolver resolver,
        RequestId requestId)
    {
        var channel = message.Channel;
        var guildChannel = channel as SocketGuildChannel;
        return resolver.Resolve(
            new DiscordContainer(
                guildChannel?.Guild.Id.ToString(CultureInfo.InvariantCulture) ?? "dm",
                channel.Id.ToString(CultureInfo.InvariantCulture),
                null,
                message.Author.Id.ToString(CultureInfo.InvariantCulture),
                guildChannel is null),
            requestId,
            requestId);
    }

    public static TenantContext Resolve(
        SocketInteraction interaction,
        IDiscordTenantContextResolver resolver,
        RequestId requestId)
    {
        var channel = interaction.Channel;
        var guildChannel = channel as SocketGuildChannel;
        return resolver.Resolve(
            new DiscordContainer(
                guildChannel?.Guild.Id.ToString(CultureInfo.InvariantCulture) ?? "dm",
                channel?.Id.ToString(CultureInfo.InvariantCulture) ?? "dm",
                null,
                interaction.User.Id.ToString(CultureInfo.InvariantCulture),
                guildChannel is null),
            requestId,
            requestId);
    }
}
