using System.Globalization;
using Telegram.Bot.Types;

namespace BotFramework.Sdk.MiniGames;

/// <summary>
/// Resolves the human player for Telegram mini-game dice: user-sent dice use <see cref="Message.From"/>;
/// bot-sent dice (after <c>/game bet</c>) must reply to the user's command so we read <see cref="Message.ReplyToMessage"/>.
/// </summary>
public static class MiniGameDicePlayer
{
    public static bool TryResolvePlayer(Message diceMessage, out long userId, out string displayName)
    {
        userId = 0;
        displayName = "";
        if (diceMessage.From is { IsBot: false } u)
        {
            userId = u.Id;
            displayName = Format(u);
            return true;
        }

        if (diceMessage is not { From.IsBot: true, ReplyToMessage.From: { IsBot: false } ru }) return false;
        userId = ru.Id;
        displayName = Format(ru);
        return true;

    }

    private static string Format(User u) =>
        u.Username ?? u.FirstName ?? string.Create(CultureInfo.InvariantCulture, $"User ID: {u.Id}");
}
