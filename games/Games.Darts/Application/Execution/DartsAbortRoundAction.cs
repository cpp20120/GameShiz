using BotFramework.Contracts.Messaging;
using BotFramework.Sdk.Execution;

namespace Games.Darts.Application.Execution;

public sealed class DartsAbortRoundAction
    : IGameAction<DartsAbortRoundCommand, DartsQueuedState, DartsAbortRoundResult>
{
    public GameDecision<DartsQueuedState, DartsAbortRoundResult> Decide(
        GameActionInput<DartsQueuedState, DartsAbortRoundCommand> input)
    {
        if (input.State.Round is not { Status: DartsRoundStatus.Queued } round
            || round.UserId != input.Command.UserId
            || round.ChatId != input.Command.ChatId)
        {
            return new(DecisionStatus.Rejected, input.State, new(false), [], [], [], [], [], "no_queued_round");
        }
        var quota = DartsPlaceBetAction.RequiredQuota(input.Quotas);
        return new(
            DecisionStatus.Accepted,
            new DartsQueuedState(null, 0),
            new DartsAbortRoundResult(true),
            [EconomyEffect.Credit(round.Amount, "darts.bet_reply_failed.refund")],
            quota.Limit > 0 ? [QuotaEffect.Restore(DartsPlaceBetAction.DailyRollQuota)] : [],
            [],
            [new DartsBetAborted(round.UserId, round.ChatId, round.Amount, round.Id,
                input.UtcNow.ToUnixTimeMilliseconds())],
            []);
    }
}
