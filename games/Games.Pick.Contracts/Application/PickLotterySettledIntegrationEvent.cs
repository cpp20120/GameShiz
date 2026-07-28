using BotFramework.Contracts.Messaging;

namespace Games.Pick.Application.Services;

public sealed record PickLotterySettledIntegrationEvent(
    string EventType,
    DateTimeOffset OccurredAt,
    Guid LotteryId,
    long ChatId,
    LotterySettleKind Kind,
    int Stake,
    int Entrants,
    long? WinnerId,
    string? WinnerName,
    int Pot,
    int Fee,
    int Payout) : IIntegrationEvent;
