using BotFramework.Host.Composition.Builder;
using ChatAdministration.Domain.Models;
using ChatAdministration.Telegram.Presentation;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

public sealed class TelegramRoleResolverTests
{
    [Fact]
    public async Task ConfiguredAdminIsOwnerInPrivateChatWithoutTelegramLookup()
    {
        const long adminId = 925337014;
        var options = Options.Create(new BotFrameworkOptions { Admins = [adminId] });

        var role = await TelegramRoleResolver.ResolveAsync(
            bot: null!,
            options.Value,
            adminId,
            adminId,
            CancellationToken.None);

        Assert.Equal(ChatMemberRole.Owner, role);
    }
}
