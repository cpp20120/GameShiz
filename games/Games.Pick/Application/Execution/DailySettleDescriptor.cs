using BotFramework.Host.Execution;
namespace Games.Pick.Application.Execution;
public sealed class DailySettleDescriptor : GameExecutionDescriptor<DailySettleCommand, DailyLotteryState, DailySettleResult>
{
    public override string GameId => "pick-daily";
    public override bool UsesPrimaryWallet => false;
    public override IReadOnlyList<string> EntropyNames => [DailySettleAction.WinnerEntropy];
    public override string CommandId(DailySettleCommand command) => command.CommandId;
    public override string AggregateId(DailySettleCommand command) =>
        $"{command.Row.ChatId}:{command.Row.DayLocal:yyyy-MM-dd}";
    public override long ChatId(DailySettleCommand command) => command.Row.ChatId;
    public override string DisplayName(DailySettleCommand command) => "daily lottery";
    public override WalletIdentity Wallet(DailySettleCommand command) => new(0, command.Row.ChatId);
    public override IReadOnlyList<string> AdditionalLockKeys(DailySettleCommand command) =>
        command.ExpectedTickets
            .Select(ticket => new WalletIdentity(ticket.UserId, command.Row.ChatId).LockKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
