using BotFramework.Discord;
using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Discord.WebSocket;
using Games.Admin.Application.Services;
using Microsoft.Extensions.Options;

namespace Games.Admin.Discord;

public sealed class AdminDiscordHandler(IAdminService service, IOptions<DiscordOptions> options) : IDiscordMessageHandler
{
    public bool CanHandle(DiscordMessageContext context) => DiscordCommand.Is(context, "admin");

    public async Task HandleAsync(DiscordMessageContext context)
    {
        var configuration = options.Value;
        if (!IsAllowed(context, configuration))
        {
            service.ReportNotAdmin(DiscordCommand.UserId(context));
            await DiscordCommand.ReplyAsync(context, "Недостаточно прав.");
            return;
        }

        var callerId = configuration.AdminActorId != 0 ? configuration.AdminActorId : DiscordCommand.UserId(context);
        var parts = DiscordCommand.Parts(context);
        if (parts.Length < 2)
        {
            await Usage(context);
            return;
        }

        switch (parts[1].ToLowerInvariant())
        {
            case "sync":
                await DiscordCommand.ReplyAsync(context, $"Синхронизировано пользователей: {await service.UserSyncAsync(callerId, context.CancellationToken)}");
                return;
            case "user" when parts.Length >= 3 && long.TryParse(parts[2], out var userId):
            {
                var scopeId = parts.Length >= 4 && long.TryParse(parts[3], out var parsedScope)
                    ? parsedScope : DiscordCommand.ScopeId(context);
                await DiscordCommand.ReplyResultAsync(context,
                    await service.GetUserAsync(userId, scopeId, context.CancellationToken), "Admin user");
                return;
            }
            case "pay" when parts.Length >= 5 && long.TryParse(parts[2], out var targetId)
                && long.TryParse(parts[3], out var balanceScopeId) && int.TryParse(parts[4], out var amount):
                await DiscordCommand.ReplyResultAsync(context,
                    await service.PayAsync(callerId, targetId, balanceScopeId, amount, context.CancellationToken), "Admin pay");
                return;
            case "clearbets":
            {
                var chatId = parts.Length >= 3 && long.TryParse(parts[2], out var parsedChat)
                    ? parsedChat : DiscordCommand.ScopeId(context);
                await DiscordCommand.ReplyResultAsync(context,
                    await service.ClearChatBetsAsync(callerId, chatId, context.CancellationToken), "Admin clear bets");
                return;
            }
            case "rename" when parts.Length >= 4:
                await DiscordCommand.ReplyResultAsync(context,
                    await service.RenameAsync(callerId, parts[2], parts[3], context.CancellationToken), "Admin rename");
                return;
            default:
                await Usage(context);
                return;
        }
    }

    private static bool IsAllowed(DiscordMessageContext context, DiscordOptions options)
    {
        if (options.AdminUserIds.Contains(context.Message.Author.Id)) return true;
        return context.Message.Author is SocketGuildUser guildUser
            && guildUser.Roles.Any(role => options.AdminRoleIds.Contains(role.Id));
    }

    private static Task Usage(DiscordMessageContext context) => DiscordCommand.ReplyAsync(context,
        "`admin sync` | `admin user <id> [scope]` | `admin pay <id> <scope> <amount>` | `admin clearbets [scope]` | `admin rename <old> <new>`");
}

public static class AdminDiscordModule
{
    public static IServiceCollection AddAdminDiscord(this IServiceCollection services) =>
        services.AddScoped<IDiscordMessageHandler, AdminDiscordHandler>();
}
