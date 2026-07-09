using System.Net;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Games.Challenges.Application.Models;

internal static class ChallengeResultExtensions
{
    extension(Challenge _)
    {
        public static string ChallengerRollLabel(int roll) => roll.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public string TargetRollLabel(int roll) => roll.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
