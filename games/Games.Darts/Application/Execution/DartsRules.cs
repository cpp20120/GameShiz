using BotFramework.Contracts.Messaging;
using BotFramework.Sdk.Execution;

namespace Games.Darts.Application.Execution;

public static class DartsRules
{
    public static IReadOnlyDictionary<int, int> Multipliers { get; } =
        new Dictionary<int, int> { [1] = 0, [2] = 0, [3] = 0, [4] = 1, [5] = 2, [6] = 2 };

    public static int Multiplier(int face) => Multipliers.GetValueOrDefault(face);
}
