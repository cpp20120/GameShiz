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

public sealed class BowlingPlaceBetDescriptor(IRuntimeTuningAccessor tuning, IOptions<BotFrameworkOptions> options)
    : BowlingExecutionDescriptor<BowlingPlaceBetCommand, BowlingBetResult>(tuning, options)
{
    public override string CommandId(BowlingPlaceBetCommand command) => command.CommandId;
    public override string AggregateId(BowlingPlaceBetCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(BowlingPlaceBetCommand command) => command.ChatId;
    public override string DisplayName(BowlingPlaceBetCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(BowlingPlaceBetCommand command) => new(command.UserId, command.ChatId);
    protected override long UserId(BowlingPlaceBetCommand command) => command.UserId;
}

public sealed class BowlingRollDescriptor(IRuntimeTuningAccessor tuning, IOptions<BotFrameworkOptions> options)
    : BowlingExecutionDescriptor<BowlingRollCommand, BowlingRollResult>(tuning, options)
{
    public override IReadOnlyList<string> EntropyNames => [BowlingRollAction.RedeemDropEntropy];
    public override string CommandId(BowlingRollCommand command) => command.CommandId;
    public override string AggregateId(BowlingRollCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(BowlingRollCommand command) => command.ChatId;
    public override string DisplayName(BowlingRollCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(BowlingRollCommand command) => new(command.UserId, command.ChatId);
    protected override long UserId(BowlingRollCommand command) => command.UserId;
}

public sealed class BowlingAbortDescriptor(IRuntimeTuningAccessor tuning, IOptions<BotFrameworkOptions> options)
    : BowlingExecutionDescriptor<BowlingAbortCommand, BowlingAbortResult>(tuning, options)
{
    public override string CommandId(BowlingAbortCommand command) => command.CommandId;
    public override string AggregateId(BowlingAbortCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(BowlingAbortCommand command) => command.ChatId;
    public override string DisplayName(BowlingAbortCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(BowlingAbortCommand command) => new(command.UserId, command.ChatId);
    protected override long UserId(BowlingAbortCommand command) => command.UserId;
}
