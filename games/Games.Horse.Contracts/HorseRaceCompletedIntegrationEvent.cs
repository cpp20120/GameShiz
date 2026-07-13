using BotFramework.Contracts.Messaging;

namespace Games.Horse.Contracts;

public sealed record HorseRaceCompletedIntegrationEvent(
    string EventType,
    DateTimeOffset OccurredAt,
    string RaceDate,
    RaceOutcome Outcome) : IIntegrationEvent;
