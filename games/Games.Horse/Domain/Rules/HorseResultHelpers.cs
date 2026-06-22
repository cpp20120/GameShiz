namespace Games.Horse.Domain.Rules;

public static class HorseResultHelpers
{
    public static BetResult BetFail(HorseError e, int horseId = 0, int coins = 0) => new(e, horseId, 0, coins);
    public static RaceOutcome RaceFail(HorseError e) => new(e, -1, [], [], [], []);
}
