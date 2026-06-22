namespace Games.Meta.Domain.Seasons;

public sealed record SeasonProfile(
    MetaSeason Season,
    SeasonPlayer Player,
    string Division,
    long NextLevelXp,
    long CurrentLevelXpFloor);
