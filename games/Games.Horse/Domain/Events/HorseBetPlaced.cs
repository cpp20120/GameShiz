using BotFramework.Sdk;

namespace Games.Horse;

public sealed record HorseBetPlaced(
    long UserId,
    int HorseId,
    int Amount,
    string RaceDate,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "horse.bet_placed";
}
