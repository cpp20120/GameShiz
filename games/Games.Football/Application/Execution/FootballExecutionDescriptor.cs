using BotFramework.Host.Execution;
using Microsoft.Extensions.Options;

namespace Games.Football.Application.Execution;

public abstract class FootballExecutionDescriptor<TCommand, TResult>(
    IRuntimeTuningAccessor tuning,
    IOptions<BotFrameworkOptions> botOptions)
    : GameExecutionDescriptor<TCommand, FootballBetState, TResult>
{
    public override string GameId => MiniGameIds.Football;

    public override IReadOnlyList<QuotaIdentity> Quotas(TCommand command, DateTimeOffset utcNow)
    {
        var userId = UserId(command);
        var options = tuning.TelegramDiceDailyLimit;
        var limit = userId == ChatId(command) && botOptions.Value.Admins.Contains(userId)
            ? 0
            : options.GetMaxRollsPerUserPerDay(GameId);
        var localDate = DateOnly.FromDateTime(utcNow.AddHours(options.TimezoneOffsetHours).DateTime);
        return [new(FootballPlaceBetAction.DailyRollQuota, GameId, userId, ChatId(command), localDate, limit)];
    }

    protected abstract long UserId(TCommand command);
}
