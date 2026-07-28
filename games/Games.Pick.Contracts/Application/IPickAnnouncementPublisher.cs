using BotFramework.Contracts.Messaging;

namespace Games.Pick.Application.Services;

public interface IPickAnnouncementPublisher
{
    Task PublishLotteryAsync(LotterySettleResult result, CancellationToken ct);
    Task PublishDailyAsync(DailySettleResult result, CancellationToken ct);
}
