namespace SampleGame.Domain;

public sealed record SampleGameState(int Version = 0)
{
    public static SampleGameState Empty { get; } = new();
}

public static class SampleGameRules
{
    public static SampleGameState Apply(SampleGameState state) =>
        state with { Version = checked(state.Version + 1) };
}
