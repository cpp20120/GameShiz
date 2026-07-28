using System.Globalization;
using BotFramework.Host.Execution;

namespace Games.Pick.Application.Execution;

public sealed class QuickOpenDescriptor : QuickDescriptor<QuickLotteryOpenCommand, LotteryOpenResult>
{
    public override string CommandId(QuickLotteryOpenCommand command) => command.CommandId;
    public override string AggregateId(QuickLotteryOpenCommand command) =>
        command.ChatId.ToString(CultureInfo.InvariantCulture);
    public override long ChatId(QuickLotteryOpenCommand command) => command.ChatId;
    public override string DisplayName(QuickLotteryOpenCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(QuickLotteryOpenCommand command) =>
        new(command.UserId, command.ChatId);
}
