using BotFramework.Sdk.Execution;

namespace Games.DiceCube.Application.Execution;

public sealed class DiceCubeAbortAction
    : IGameAction<DiceCubeAbortCommand, DiceCubePlaceBetState, DiceCubeAbortResult>
{
    public GameDecision<DiceCubePlaceBetState, DiceCubeAbortResult> Decide(
        GameActionInput<DiceCubePlaceBetState, DiceCubeAbortCommand> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.State.PendingBet is not { } bet)
        {
            return new GameDecision<DiceCubePlaceBetState, DiceCubeAbortResult>(
                DecisionStatus.Rejected,
                input.State,
                new DiceCubeAbortResult(false),
                [], [], [], [], [], "no_pending_bet");
        }
        if (!input.Quotas.TryGetValue(DiceCubePlaceBetAction.DailyRollQuota, out var quota))
            throw new InvalidOperationException($"Required quota '{DiceCubePlaceBetAction.DailyRollQuota}' was not supplied.");

        return new GameDecision<DiceCubePlaceBetState, DiceCubeAbortResult>(
            DecisionStatus.Accepted,
            new DiceCubePlaceBetState(null),
            new DiceCubeAbortResult(true),
            [EconomyEffect.Credit(bet.Amount, "dicecube.bot_dice.failed")],
            quota.Limit > 0 ? [QuotaEffect.Restore(DiceCubePlaceBetAction.DailyRollQuota)] : [],
            [],
            [new DiceCubeBetAborted(
                input.Command.UserId,
                input.Command.ChatId,
                bet.Amount,
                input.UtcNow.ToUnixTimeMilliseconds())],
            []);
    }
}
