using BotFramework.Rendering;
using Games.Horse.Application.Jobs;
using Games.Horse.Domain.Configuration;
using Games.Horse.Rendering;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class HorseRenderingTests
{
    [Fact]
    public void DeterministicSpeedSeed_ProducesIdenticalRace()
    {
        var first = SpeedGenerator.CreateSpeeds(4, 2, "race-seed");
        var second = SpeedGenerator.CreateSpeeds(4, 2, "race-seed");

        Assert.Equal(first.Length, second.Length);
        for (var index = 0; index < first.Length; index++)
            Assert.Equal(first[index], second[index]);
    }

    [Fact]
    public void RenderKey_ChangesWithVisualVariant()
    {
        var job = new HorseRaceRenderJob();

        var first = job.Describe(new HorseRaceRenderSpec(4, 2, 0));
        var second = job.Describe(new HorseRaceRenderSpec(4, 2, 1));

        Assert.NotEqual(first.ContentHash, second.ContentHash);
        Assert.Equal("horse-race", first.RendererId);
        Assert.Equal("gif", first.Extension);
    }

    [Fact]
    public async Task QuartzPrewarm_QueuesCompleteWinnerVariantMatrix()
    {
        var queue = new CapturingRenderQueue();
        var command = new HorseRenderPrewarmScheduledCommand(
            queue,
            Options.Create(new HorseOptions { HorseCount = 4, RenderVariants = 3 }));

        await command.ExecuteAsync(new Dictionary<string, string>(), CancellationToken.None);

        Assert.Equal(12, queue.Specs.Count);
        Assert.Equal(4, queue.Specs.Select(static spec => spec.Winner).Distinct().Count());
        Assert.Equal(3, queue.Specs.Select(static spec => spec.Variant).Distinct().Count());
    }

    private sealed class CapturingRenderQueue : IRenderQueue
    {
        public List<HorseRaceRenderSpec> Specs { get; } = [];

        public ValueTask<RenderedArtifact> GetOrRenderAsync<TSpec>(
            TSpec spec,
            RenderPriority priority = RenderPriority.Interactive,
            CancellationToken ct = default) =>
            ValueTask.FromException<RenderedArtifact>(new NotSupportedException());

        public Task PrewarmAsync<TSpec>(IEnumerable<TSpec> specs, CancellationToken ct = default)
        {
            Specs.AddRange(specs.Cast<HorseRaceRenderSpec>());
            return Task.CompletedTask;
        }
    }
}
