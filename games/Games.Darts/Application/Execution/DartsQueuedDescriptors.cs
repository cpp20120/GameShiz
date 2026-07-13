using BotFramework.Host.Execution;
using Microsoft.Extensions.Options;

namespace Games.Darts.Application.Execution;

public abstract class DartsQueuedDescriptor<TCommand, TResult>(
    IRuntimeTuningAccessor tuning,
    IOptions<BotFrameworkOptions> botOptions)
    : GameExecutionDescriptor<TCommand, DartsQueuedState, TResult>
{
    public override string GameId => MiniGameIds.Darts;

    public override IReadOnlyList<QuotaIdentity> Quotas(TCommand command, DateTimeOffset utcNow)
    {
        var userId = UserId(command);
        var options = tuning.TelegramDiceDailyLimit;
        var unlimitedAdmin = userId == ChatId(command) && botOptions.Value.Admins.Contains(userId);
        var limit = unlimitedAdmin ? 0 : options.GetMaxRollsPerUserPerDay(GameId);
        var localDate = DateOnly.FromDateTime(utcNow.AddHours(options.TimezoneOffsetHours).DateTime);
        return [new(DartsPlaceBetAction.DailyRollQuota, GameId, userId, ChatId(command), localDate, limit)];
    }

    protected abstract long UserId(TCommand command);
}

public sealed class DartsPlaceBetDescriptor(IRuntimeTuningAccessor tuning, IOptions<BotFrameworkOptions> botOptions)
    : DartsQueuedDescriptor<DartsPlaceBetCommand, DartsBetResult>(tuning, botOptions)
{
    public override string CommandId(DartsPlaceBetCommand command) => command.CommandId;
    public override string AggregateId(DartsPlaceBetCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(DartsPlaceBetCommand command) => command.ChatId;
    public override string DisplayName(DartsPlaceBetCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(DartsPlaceBetCommand command) => new(command.UserId, command.ChatId);
    protected override long UserId(DartsPlaceBetCommand command) => command.UserId;
}

public sealed class DartsResolveRoundDescriptor(IRuntimeTuningAccessor tuning, IOptions<BotFrameworkOptions> botOptions)
    : DartsQueuedDescriptor<DartsResolveRoundCommand, DartsThrowResult>(tuning, botOptions)
{
    public override IReadOnlyList<string> EntropyNames => [DartsResolveRoundAction.RedeemDropEntropy];
    public override string CommandId(DartsResolveRoundCommand command) => command.CommandId;
    public override string AggregateId(DartsResolveRoundCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(DartsResolveRoundCommand command) => command.ChatId;
    public override string DisplayName(DartsResolveRoundCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(DartsResolveRoundCommand command) => new(command.UserId, command.ChatId);
    protected override long UserId(DartsResolveRoundCommand command) => command.UserId;
}

public sealed class DartsAbortRoundDescriptor(IRuntimeTuningAccessor tuning, IOptions<BotFrameworkOptions> botOptions)
    : DartsQueuedDescriptor<DartsAbortRoundCommand, DartsAbortRoundResult>(tuning, botOptions)
{
    public override string CommandId(DartsAbortRoundCommand command) => command.CommandId;
    public override string AggregateId(DartsAbortRoundCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(DartsAbortRoundCommand command) => command.ChatId;
    public override string DisplayName(DartsAbortRoundCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(DartsAbortRoundCommand command) => new(command.UserId, command.ChatId);
    protected override long UserId(DartsAbortRoundCommand command) => command.UserId;
}
