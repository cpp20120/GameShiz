using BotFramework.Host.Execution;
using Microsoft.Extensions.Options;

namespace Games.Basketball.Application.Execution;

public abstract class BasketballExecutionDescriptor<TCommand, TResult>(
    IRuntimeTuningAccessor tuning,
    IOptions<BotFrameworkOptions> botOptions)
    : GameExecutionDescriptor<TCommand, BasketballBetState, TResult>
{
    public override string GameId => MiniGameIds.Basketball;

    public override IReadOnlyList<QuotaIdentity> Quotas(TCommand command, DateTimeOffset utcNow)
    {
        var userId = UserId(command);
        var options = tuning.TelegramDiceDailyLimit;
        var unlimitedAdmin = userId == ChatId(command) && botOptions.Value.Admins.Contains(userId);
        var limit = unlimitedAdmin ? 0 : options.GetMaxRollsPerUserPerDay(GameId);
        var localDate = DateOnly.FromDateTime(utcNow.AddHours(options.TimezoneOffsetHours).DateTime);
        return [new(
            BasketballPlaceBetAction.DailyRollQuota,
            GameId,
            userId,
            ChatId(command),
            localDate,
            limit)];
    }

    protected abstract long UserId(TCommand command);
}
