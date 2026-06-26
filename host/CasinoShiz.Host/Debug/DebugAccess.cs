using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace CasinoShiz.Host.Debug;

internal static class DebugAccess
{
    public static bool IsAllowed(Message msg, IConfiguration configuration, BotFrameworkOptions options)
    {
        if (!configuration.GetValue("Debug:Enabled", defaultValue: true)) return false;
        if (!configuration.GetValue("Debug:RequireAdmin", defaultValue: true)) return true;
        var userId = msg.From?.Id ?? 0;
        return options.Admins.Any(id => id == userId) || options.ReadOnlyAdmins.Any(id => id == userId);
    }
}
