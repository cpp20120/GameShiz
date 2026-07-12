namespace Games.Football.Application.Execution;

public static class FootballRules
{
    public static readonly IReadOnlyDictionary<int, int> Multipliers = new Dictionary<int, int>
    {
        [1] = 0, [2] = 0, [3] = 0, [4] = 2, [5] = 2,
    };

    public static int Multiplier(int face) => Multipliers.GetValueOrDefault(face);
}
