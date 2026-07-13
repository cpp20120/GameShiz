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

public sealed class FootballPlaceBetDescriptor(IRuntimeTuningAccessor tuning, IOptions<BotFrameworkOptions> options)
    : FootballExecutionDescriptor<FootballPlaceBetCommand, FootballBetResult>(tuning, options)
{
    public override string CommandId(FootballPlaceBetCommand command) => command.CommandId;
    public override string AggregateId(FootballPlaceBetCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(FootballPlaceBetCommand command) => command.ChatId;
    public override string DisplayName(FootballPlaceBetCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(FootballPlaceBetCommand command) => new(command.UserId, command.ChatId);
    protected override long UserId(FootballPlaceBetCommand command) => command.UserId;
}

public sealed class FootballThrowDescriptor(IRuntimeTuningAccessor tuning, IOptions<BotFrameworkOptions> options)
    : FootballExecutionDescriptor<FootballThrowCommand, FootballThrowResult>(tuning, options)
{
    public override IReadOnlyList<string> EntropyNames => [FootballThrowAction.RedeemDropEntropy];
    public override string CommandId(FootballThrowCommand command) => command.CommandId;
    public override string AggregateId(FootballThrowCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(FootballThrowCommand command) => command.ChatId;
    public override string DisplayName(FootballThrowCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(FootballThrowCommand command) => new(command.UserId, command.ChatId);
    protected override long UserId(FootballThrowCommand command) => command.UserId;
}

public sealed class FootballAbortDescriptor(IRuntimeTuningAccessor tuning, IOptions<BotFrameworkOptions> options)
    : FootballExecutionDescriptor<FootballAbortCommand, FootballAbortResult>(tuning, options)
{
    public override string CommandId(FootballAbortCommand command) => command.CommandId;
    public override string AggregateId(FootballAbortCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(FootballAbortCommand command) => command.ChatId;
    public override string DisplayName(FootballAbortCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(FootballAbortCommand command) => new(command.UserId, command.ChatId);
    protected override long UserId(FootballAbortCommand command) => command.UserId;
}
