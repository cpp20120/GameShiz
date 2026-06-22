namespace Games.Meta;

public interface ISeasonRewardService
{
    Task<SeasonRewardProcessResult> ProcessPlayerRewardsAsync(long seasonId, CancellationToken ct);
    Task<SeasonRewardProcessResult> ProcessClanRewardsAsync(long seasonId, CancellationToken ct);
}
