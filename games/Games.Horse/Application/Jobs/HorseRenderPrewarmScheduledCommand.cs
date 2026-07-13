using BotFramework.Rendering;
using BotFramework.Scheduling.Abstractions;
using Games.Horse.Rendering;
using Microsoft.Extensions.Options;

namespace Games.Horse.Application.Jobs;

/// <summary>Repairs the deterministic Horse GIF matrix in the shared artifact store.</summary>
public sealed class HorseRenderPrewarmScheduledCommand(
    IRenderQueue renders,
    IOptions<HorseOptions> options) : IRecurringScheduledCommand
{
    public const string CommandKey = "horse.render-prewarm";

    public string Key => CommandKey;

    public ScheduleDescriptor Schedule => new("0 15 3 * * ?");

    public Task ExecuteAsync(IReadOnlyDictionary<string, string> data, CancellationToken ct)
    {
        var value = options.Value;
        var specs = Enumerable.Range(0, value.HorseCount)
            .SelectMany(winner => Enumerable.Range(0, Math.Max(1, value.RenderVariants))
                .Select(variant => new HorseRaceRenderSpec(value.HorseCount, winner, variant)));
        return renders.PrewarmAsync(specs, ct);
    }
}
