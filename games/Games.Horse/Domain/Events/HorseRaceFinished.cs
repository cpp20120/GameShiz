
namespace Games.Horse.Domain.Events;

public sealed record HorseRaceFinished(
    string RaceDate,
    int Winner,
    int BetsCount,
    int PayoutCount,
    int Pot,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "horse.race_finished";
}
