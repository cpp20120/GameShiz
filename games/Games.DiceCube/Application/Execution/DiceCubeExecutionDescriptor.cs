using BotFramework.Host.Execution;
using Microsoft.Extensions.Options;

namespace Games.DiceCube.Application.Execution;

public abstract class DiceCubeExecutionDescriptor<TCommand, TResult>(
    IRuntimeTuningAccessor tuning,
    IOptions<BotFrameworkOptions> botOptions)
    : GameExecutionDescriptor<TCommand, DiceCubePlaceBetState, TResult>
{
    public override string GameId => MiniGameIds.DiceCube;

    public override IReadOnlyList<QuotaIdentity> Quotas(TCommand command, DateTimeOffset utcNow)
    {
        var userId = UserId(command);
        var options = tuning.TelegramDiceDailyLimit;
        var unlimitedAdmin = userId == ChatId(command) && botOptions.Value.Admins.Contains(userId);
        var limit = unlimitedAdmin ? 0 : options.GetMaxRollsPerUserPerDay(GameId);
        var localDate = DateOnly.FromDateTime(utcNow.AddHours(options.TimezoneOffsetHours).DateTime);
        return [new(DiceCubePlaceBetAction.DailyRollQuota, GameId, userId, ChatId(command), localDate, limit)];
    }

    protected abstract long UserId(TCommand command);
}
