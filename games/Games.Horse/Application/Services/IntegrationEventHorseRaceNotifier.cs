using BotFramework.Contracts.Messaging;
using Games.Horse.Contracts;

namespace Games.Horse.Application.Services;

public sealed class IntegrationEventHorseRaceNotifier(IIntegrationEventPublisher events)
    : IHorseRaceNotifier
{
    public Task SendResultGifsAsync(RaceOutcome outcome, string raceDate, CancellationToken ct) =>
        events.PublishAsync(new HorseRaceCompletedIntegrationEvent(
            "horse.race.completed.v1", DateTimeOffset.UtcNow, raceDate, outcome), ct);

    public void ScheduleWinnerAnnouncements(RaceOutcome outcome)
    {
    }
}
