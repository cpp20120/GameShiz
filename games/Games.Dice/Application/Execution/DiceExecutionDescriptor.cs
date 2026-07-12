using System.Globalization;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Microsoft.Extensions.Options;

namespace Games.Dice.Application.Execution;

public sealed class DiceExecutionDescriptor(
    IRuntimeTuningAccessor tuning,
    IOptions<BotFrameworkOptions> botOptions)
    : GameExecutionDescriptor<DiceCommand, NoGameState, DicePlayResult>
{
    public override string GameId => MiniGameIds.Dice;

    public override IReadOnlyList<string> EntropyNames => [DiceAction.RedeemDropEntropy];

    public override string CommandId(DiceCommand command) => string.Create(
        CultureInfo.InvariantCulture,
        $"dice:roll:{command.ChatId}:{command.SourceMessageId}:{command.UserId}");

    public override string AggregateId(DiceCommand command) => string.Create(
        CultureInfo.InvariantCulture,
        $"{command.ChatId}:{command.UserId}");

    public override long ChatId(DiceCommand command) => command.ChatId;

    public override string DisplayName(DiceCommand command) => command.DisplayName;

    public override WalletIdentity Wallet(DiceCommand command) => new(command.UserId, command.ChatId);

    public override IReadOnlyList<QuotaIdentity> Quotas(DiceCommand command, DateTimeOffset utcNow)
    {
        var options = tuning.TelegramDiceDailyLimit;
        var unlimitedAdmin = command.UserId == command.ChatId && botOptions.Value.Admins.Contains(command.UserId);
        var limit = unlimitedAdmin ? 0 : options.GetMaxRollsPerUserPerDay(GameId);
        var localDate = DateOnly.FromDateTime(utcNow.AddHours(options.TimezoneOffsetHours).DateTime);
        return
        [
            new QuotaIdentity(
                DiceAction.DailyRollQuota,
                GameId,
                command.UserId,
                command.ChatId,
                localDate,
                limit),
        ];
    }
}
