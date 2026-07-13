using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Microsoft.Extensions.Options;

namespace Games.Darts.Application.Execution;

public sealed class DartsQuickThrowDescriptor(
    IRuntimeTuningAccessor tuning,
    IOptions<BotFrameworkOptions> botOptions)
    : GameExecutionDescriptor<DartsQuickThrowCommand, NoGameState, DartsThrowResult>
{
    public override string GameId => MiniGameIds.Darts;
    public override IReadOnlyList<string> EntropyNames => [DartsQuickThrowAction.RedeemDropEntropy];
    public override string CommandId(DartsQuickThrowCommand command) =>
        $"darts:quick:{command.ChatId}:{command.DiceMessageId}:{command.UserId}";
    public override string AggregateId(DartsQuickThrowCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(DartsQuickThrowCommand command) => command.ChatId;
    public override string DisplayName(DartsQuickThrowCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(DartsQuickThrowCommand command) => new(command.UserId, command.ChatId);

    public override IReadOnlyList<QuotaIdentity> Quotas(DartsQuickThrowCommand command, DateTimeOffset utcNow)
    {
        var options = tuning.TelegramDiceDailyLimit;
        var unlimitedAdmin = command.UserId == command.ChatId && botOptions.Value.Admins.Contains(command.UserId);
        var limit = unlimitedAdmin ? 0 : options.GetMaxRollsPerUserPerDay(GameId);
        var localDate = DateOnly.FromDateTime(utcNow.AddHours(options.TimezoneOffsetHours).DateTime);
        return [new(DartsQuickThrowAction.DailyRollQuota, GameId, command.UserId, command.ChatId, localDate, limit)];
    }
}
