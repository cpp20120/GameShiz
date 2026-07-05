
namespace Games.Horse.Application.Services;

public interface IHorseRaceNotifier
{
    Task SendResultGifsAsync(RaceOutcome outcome, string raceDate, CancellationToken ct);
    void ScheduleWinnerAnnouncements(RaceOutcome outcome);
}
