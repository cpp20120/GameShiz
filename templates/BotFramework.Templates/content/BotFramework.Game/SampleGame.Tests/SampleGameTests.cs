using SampleGame.Domain;
using Xunit;

namespace SampleGame.Tests;

public sealed class SampleGameTests
{
    [Fact]
    public void Domain_decision_is_deterministic()
    {
        Assert.Equal(1, SampleGameRules.Apply(new SampleGameState()).Version);
    }
}
