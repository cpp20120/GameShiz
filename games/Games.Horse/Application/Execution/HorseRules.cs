using BotFramework.Sdk.Execution;
using static Games.Horse.Domain.Rules.HorseResultHelpers;

namespace Games.Horse.Application.Execution;

public static class HorseRules
{
    public static IReadOnlyDictionary<int, double> GetCoefficients(IReadOnlyDictionary<int, int> stakes)
    {
        var sum = stakes.Values.Sum();
        return stakes.ToDictionary(
            pair => pair.Key,
            pair => pair.Value == 0
                ? 1.0
                : Math.Floor((sum - pair.Value) / (1.1 * pair.Value) * 1000) / 1000 + 1);
    }
}
