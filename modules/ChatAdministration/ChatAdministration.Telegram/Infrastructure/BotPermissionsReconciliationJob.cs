using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Models;
using BotFramework.Sdk.Modules;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ChatAdministration.Telegram.Infrastructure;

public sealed class BotPermissionsReconciliationJob(
    IChatAdministrationStore store,
    ITelegramBotClient bot) : IBackgroundJob
{
    public string Name => "chat_administration.bot_permissions_reconciliation";

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ReconcileOnceAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task ReconcileOnceAsync(CancellationToken ct)
    {
        User botUser;
        try
        {
            botUser = await bot.GetMe(ct);
        }
        catch (ApiRequestException)
        {
            return;
        }

        foreach (var chat in await store.ListEnabledChatsAsync(ct))
        {
            try
            {
                var member = await bot.GetChatMember(chat.Id.Value, botUser.Id, ct);
                var permissions = member switch
                {
                    ChatMemberOwner => AllPermissions(),
                    ChatMemberAdministrator administrator => new TelegramBotPermissions
                    {
                        CanDeleteMessages = administrator.CanDeleteMessages,
                        CanRestrictMembers = administrator.CanRestrictMembers,
                        CanInviteUsers = administrator.CanInviteUsers,
                        CanPinMessages = administrator.CanPinMessages,
                        ObservedAt = DateTimeOffset.UtcNow,
                    },
                    _ => new TelegramBotPermissions { ObservedAt = DateTimeOffset.UtcNow },
                };
                await store.UpdateBotPermissionsAsync(chat.Id, permissions, $"bot-permissions:{chat.Id}:{permissions.ObservedAt:O}", ct);
            }
            catch (ApiRequestException)
            {
                // A single inaccessible chat must not prevent reconciliation of other tenants.
            }
        }
    }

    private static TelegramBotPermissions AllPermissions() => new()
    {
        CanDeleteMessages = true,
        CanRestrictMembers = true,
        CanInviteUsers = true,
        CanPinMessages = true,
        ObservedAt = DateTimeOffset.UtcNow,
    };
}
