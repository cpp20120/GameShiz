using System.Globalization;
using BotFramework.Host.Execution;

namespace Games.Pick.Application.Execution;

public sealed class QuickJoinDescriptor : QuickDescriptor<QuickLotteryJoinCommand, LotteryJoinResult>
{
    public override string CommandId(QuickLotteryJoinCommand command) => command.CommandId;
    public override string AggregateId(QuickLotteryJoinCommand command) =>
        command.ChatId.ToString(CultureInfo.InvariantCulture);
    public override long ChatId(QuickLotteryJoinCommand command) => command.ChatId;
    public override string DisplayName(QuickLotteryJoinCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(QuickLotteryJoinCommand command) =>
        new(command.UserId, command.ChatId);
}
