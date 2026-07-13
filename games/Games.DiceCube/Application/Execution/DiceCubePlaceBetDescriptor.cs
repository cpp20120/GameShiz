using BotFramework.Host.Execution;
using Microsoft.Extensions.Options;

namespace Games.DiceCube.Application.Execution;

public sealed class DiceCubePlaceBetDescriptor(
    IRuntimeTuningAccessor tuning,
    IOptions<BotFrameworkOptions> botOptions)
    : GameExecutionDescriptor<DiceCubePlaceBetCommand, DiceCubePlaceBetState, CubeBetResult>
{
    public override string GameId => MiniGameIds.DiceCube;
    public override string CommandId(DiceCubePlaceBetCommand command) => command.CommandId;
    public override string AggregateId(DiceCubePlaceBetCommand command) => $"{command.ChatId}:{command.UserId}";
    public override long ChatId(DiceCubePlaceBetCommand command) => command.ChatId;
    public override string DisplayName(DiceCubePlaceBetCommand command) => command.DisplayName;
    public override WalletIdentity Wallet(DiceCubePlaceBetCommand command) => new(command.UserId, command.ChatId);

    public override IReadOnlyList<QuotaIdentity> Quotas(DiceCubePlaceBetCommand command, DateTimeOffset utcNow)
    {
        var options = tuning.TelegramDiceDailyLimit;
        var unlimitedAdmin = command.UserId == command.ChatId && botOptions.Value.Admins.Contains(command.UserId);
        var limit = unlimitedAdmin ? 0 : options.GetMaxRollsPerUserPerDay(GameId);
        var localDate = DateOnly.FromDateTime(utcNow.AddHours(options.TimezoneOffsetHours).DateTime);
        return [new(DiceCubePlaceBetAction.DailyRollQuota, GameId, command.UserId, command.ChatId, localDate, limit)];
    }
}
