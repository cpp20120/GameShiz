using System.Net;
using BotFramework.Host;
using BotFramework.Sdk;
using Games.Blackjack.Domain;
using Games.Horse.Generators;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Games.Challenges;

internal static class ChallengeResultExtensions
{
    public static string ChallengerRollLabel(this Challenge _, int roll) => roll.ToString();

    public static string TargetRollLabel(this Challenge _, int roll) => roll.ToString();
}
