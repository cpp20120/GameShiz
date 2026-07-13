using BotFramework.Contracts.Messaging;
using Games.Dice.Contracts.Play;
using System.Globalization;

namespace Games.Dice.Application.Requests;

public sealed class DicePlayRequestHandler(IDiceService service)
    : IRequestHandler<DicePlayRequest, DicePlayResponse>
{
    public async Task<DicePlayResponse> HandleAsync(
        DicePlayRequest request,
        RequestMetadata metadata,
        CancellationToken ct)
    {
        if (!int.TryParse(request.OperationSourceId, NumberStyles.None, CultureInfo.InvariantCulture, out var sourceId))
            throw new ArgumentException("Operation source must be a numeric id.", nameof(request));

        var result = await service.PlayAsync(
            request.UserId,
            request.DisplayName,
            request.SlotValue,
            request.BalanceScopeId,
            sourceId,
            request.IsForwarded,
            ct);

        return new DicePlayResponse(
            Map(result.Outcome),
            result.Prize,
            result.Loss,
            result.NewBalance,
            result.Gas,
            result.DailyDiceUsed,
            result.DailyDiceLimit);
    }

    private static DicePlayStatus Map(DiceOutcome outcome) => outcome switch
    {
        DiceOutcome.Played => DicePlayStatus.Played,
        DiceOutcome.Forwarded => DicePlayStatus.Forwarded,
        DiceOutcome.NotEnoughCoins => DicePlayStatus.NotEnoughCoins,
        DiceOutcome.DailyRollLimitExceeded => DicePlayStatus.DailyRollLimitExceeded,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };
}
