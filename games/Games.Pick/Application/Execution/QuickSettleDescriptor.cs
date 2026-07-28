using System.Globalization;
using BotFramework.Host.Execution;

namespace Games.Pick.Application.Execution;

public sealed class QuickSettleDescriptor : QuickDescriptor<QuickLotterySettleCommand, LotterySettleResult>
{
    public override bool UsesPrimaryWallet => false;
    public override IReadOnlyList<string> EntropyNames => [QuickLotterySettleAction.WinnerEntropy];
    public override string CommandId(QuickLotterySettleCommand command) => command.CommandId;
    public override string AggregateId(QuickLotterySettleCommand command) =>
        command.Row.ChatId.ToString(CultureInfo.InvariantCulture);
    public override long ChatId(QuickLotterySettleCommand command) => command.Row.ChatId;
    public override string DisplayName(QuickLotterySettleCommand command) => "lottery";
    public override WalletIdentity Wallet(QuickLotterySettleCommand command) =>
        new(0, command.Row.ChatId);
    public override IReadOnlyList<string> AdditionalLockKeys(QuickLotterySettleCommand command) =>
        command.ExpectedEntries
            .Select(entry => new WalletIdentity(entry.UserId, command.Row.ChatId).LockKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
