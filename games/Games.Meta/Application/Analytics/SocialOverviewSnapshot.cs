namespace Games.Meta.Application.Analytics;

internal sealed record SocialOverviewSnapshot(
    long NewChats24H,
    long ActiveChats24H,
    long Transfers24H,
    long TransferCoins24H,
    long ChallengesCreated24H,
    long SocialUsers24H);
