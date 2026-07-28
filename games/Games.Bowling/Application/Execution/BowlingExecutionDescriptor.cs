using BotFramework.Host.Execution;
using Microsoft.Extensions.Options;

namespace Games.Bowling.Application.Execution;

public abstract class BowlingExecutionDescriptor<TCommand, TResult>(
    IRuntimeTuningAccessor tuning,
    IOptions<BotFrameworkOptions> botOptions)
    : GameExecutionDescriptor<TCommand, BowlingBetState, TResult>
{
    public override string GameId => MiniGameIds.Bowling;

    public override IReadOnlyList<QuotaIdentity> Quotas(TCommand command, DateTimeOffset utcNow)
    {
        var userId = UserId(command);
        var options = tuning.TelegramDiceDailyLimit;
        var limit = userId == ChatId(command) && botOptions.Value.Admins.Contains(userId)
            ? 0
            : options.GetMaxRollsPerUserPerDay(GameId);
        var localDate = DateOnly.FromDateTime(utcNow.AddHours(options.TimezoneOffsetHours).DateTime);
        return [new(BowlingPlaceBetAction.DailyRollQuota, GameId, userId, ChatId(command), localDate, limit)];
    }

    protected abstract long UserId(TCommand command);
}
