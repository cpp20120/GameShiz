using BotFramework.Contracts.Messaging;

namespace Games.Pick.Application.Services;

public interface IPickAnnouncementPublisher
{
    Task PublishLotteryAsync(LotterySettleResult result, CancellationToken ct);
    Task PublishDailyAsync(DailySettleResult result, CancellationToken ct);
}

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
