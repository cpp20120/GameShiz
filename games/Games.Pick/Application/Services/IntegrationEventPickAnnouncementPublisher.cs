using BotFramework.Contracts.Messaging;

namespace Games.Pick.Application.Services;

public sealed class IntegrationEventPickAnnouncementPublisher(IIntegrationEventPublisher events)
    : IPickAnnouncementPublisher
{
    public Task PublishLotteryAsync(LotterySettleResult result, CancellationToken ct) =>
        events.PublishAsync(new PickLotterySettledIntegrationEvent(
            "pick.lottery.settled.v1", DateTimeOffset.UtcNow,
            result.Row.Id, result.Row.ChatId, result.Kind, result.Row.Stake,
            result.Entries.Count, result.WinnerId, result.WinnerName,
            result.Pot, result.Fee, result.Payout), ct);

    public Task PublishDailyAsync(DailySettleResult result, CancellationToken ct) =>
        events.PublishAsync(new PickDailyLotterySettledIntegrationEvent(
            "pick.daily_lottery.settled.v1", DateTimeOffset.UtcNow,
            result.Row.Id, result.Row.ChatId, result.Row.DayLocal, result.Drawn,
            result.TicketsTotal, result.DistinctUsers, result.WinnerId,
            result.WinnerName, result.WinnerTicketCount ?? 0,
            result.PotTotal, result.Fee, result.Payout), ct);
}
