namespace Games.Meta.Application.Analytics;

internal sealed record GameStateSnapshot(
    long ActiveMiniGameSessions,
    long ActiveRollGates,
    long OpenChallenges,
    long OpenLotteries,
    long LotteryPot24H,
    long OpenDailyLotteries,
    long ActivePokerTables,
    long ActivePokerSeats,
    long ActiveSecretHitlerGames,
    long ActiveSecretHitlerPlayers);
