using BotFramework.Host;
using BotFramework.Sdk;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Games.Horse.Application.Services;

public interface IHorseRaceNotifier
{
    Task SendResultGifsAsync(RaceOutcome outcome, string raceDate, CancellationToken ct);
    void ScheduleWinnerAnnouncements(RaceOutcome outcome);
}
