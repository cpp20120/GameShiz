using System.Net;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Games.Challenges.Application.Models;

internal static class ChallengeResultExtensions
{
    public static string ChallengerRollLabel(this Challenge _, int roll) => roll.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string TargetRollLabel(this Challenge _, int roll) => roll.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
