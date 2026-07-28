using BotFramework.Contracts.Messaging;

namespace Games.Pick.Application.Services;

public sealed record PickDailyLotterySettledIntegrationEvent(
    string EventType,
    DateTimeOffset OccurredAt,
    Guid LotteryId,
    long ChatId,
    DateOnly Day,
    bool Drawn,
    int Tickets,
    int DistinctUsers,
    long? WinnerId,
    string? WinnerName,
    int WinnerTickets,
    int Pot,
    int Fee,
    int Payout) : IIntegrationEvent;
