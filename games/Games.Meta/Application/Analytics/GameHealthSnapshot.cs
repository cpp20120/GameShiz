namespace Games.Meta.Application.Analytics;

internal sealed record GameHealthSnapshot(
    long StaleChallenges,
    long ChallengesCompleted24H,
    long ChallengesCancelled24H,
    long LotteriesSettled24H,
    long LotteriesCancelled24H,
    long StalePokerTables,
    long StaleSecretHitlerGames,
    long ExpiredMiniGameSessions,
    long ExpiredRollGates);
