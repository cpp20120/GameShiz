using BotFramework.Host.Composition.Builder;
using ChatAdministration.Domain.Models;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;

namespace ChatAdministration.Telegram.Presentation;

/// <summary>
/// Resolves a Telegram member role while honoring the bot-level admin list for
/// private chats. Telegram does not expose a useful group-style membership
/// status for a user's private conversation with the bot, so calling
/// <c>getChatMember</c> there would incorrectly downgrade configured admins to
/// ordinary members.
/// </summary>
public static class TelegramRoleResolver
{
    public static async Task<ChatMemberRole> ResolveAsync(
        ITelegramBotClient bot,
        BotFrameworkOptions options,
        long chatId,
        long userId,
        CancellationToken ct)
    {
        if (chatId == userId && options.Admins.Contains(userId))
            return ChatMemberRole.Owner;

        try
        {
            var member = await bot.GetChatMember(chatId, userId, ct);
            return member.Status switch
            {
                ChatMemberStatus.Creator => ChatMemberRole.Owner,
                ChatMemberStatus.Administrator => ChatMemberRole.Admin,
                _ => ChatMemberRole.Member,
            };
        }
        catch (ApiRequestException)
        {
            // A failed observation must never accidentally elevate a user.
            return ChatMemberRole.Member;
        }
    }
}
