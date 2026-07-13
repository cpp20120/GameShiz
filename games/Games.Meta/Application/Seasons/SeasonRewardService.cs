using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Games.Meta.Application.Effects;

namespace Games.Meta.Application.Seasons;

public sealed class SeasonRewardService(
    IAtomicEffectExecutor effects) : ISeasonRewardService
{
    public Task<SeasonRewardProcessResult> ProcessPlayerRewardsAsync(long seasonId, CancellationToken ct) =>
        effects.ExecuteAsync(
            new AtomicEffectExecutionEnvelope(
                "meta.season",
                $"meta:season:player-rewards:{seasonId}",
                $"season:{seasonId}",
                [$"game:meta.season:{seasonId}"]),
            new AtomicEffectPlan<SeasonRewardProcessResult>(
                new(0, []),
                [new SeasonPlayerRewardsAtomicEffect(seasonId)],
                outputs => (SeasonRewardProcessResult)outputs["result"]!),
            ct);

    public Task<SeasonRewardProcessResult> ProcessClanRewardsAsync(long seasonId, CancellationToken ct) =>
        effects.ExecuteAsync(
            new AtomicEffectExecutionEnvelope(
                "meta.season",
                $"meta:season:clan-rewards:{seasonId}",
                $"season:{seasonId}",
                [$"game:meta.season:{seasonId}"]),
            new AtomicEffectPlan<SeasonRewardProcessResult>(
                new(0, []),
                [new SeasonClanRewardsAtomicEffect(seasonId)],
                outputs => (SeasonRewardProcessResult)outputs["result"]!),
            ct);
}
