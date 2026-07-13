using BotFramework.Sdk.Execution;

namespace Games.Football.Application.Execution;

public sealed class FootballAbortAction : IGameAction<FootballAbortCommand, FootballBetState, FootballAbortResult>
{
    public GameDecision<FootballBetState, FootballAbortResult> Decide(
        GameActionInput<FootballBetState, FootballAbortCommand> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.State.PendingBet is not { } bet)
            return new(DecisionStatus.Rejected, input.State, new(false), [], [], [], [], [], "no_pending_bet");
        if (!input.Quotas.TryGetValue(FootballPlaceBetAction.DailyRollQuota, out var quota))
            throw new InvalidOperationException($"Required quota '{FootballPlaceBetAction.DailyRollQuota}' was not supplied.");
        return new(
            DecisionStatus.Accepted,
            new FootballBetState(null),
            new FootballAbortResult(true),
            [EconomyEffect.Credit(bet.Amount, "football.send_dice_failed")],
            quota.Limit > 0 ? [QuotaEffect.Restore(FootballPlaceBetAction.DailyRollQuota)] : [],
            [],
            [new FootballBetAborted(input.Command.UserId, input.Command.ChatId, bet.Amount, input.UtcNow.ToUnixTimeMilliseconds())],
            []);
    }
}
