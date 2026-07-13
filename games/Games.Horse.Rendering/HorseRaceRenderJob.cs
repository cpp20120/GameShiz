using System.Security.Cryptography;
using System.Text;
using BotFramework.Rendering;
using Games.Horse.Infrastructure.Rendering.Generators;

namespace Games.Horse.Rendering;

public sealed record HorseRaceRenderSpec(int HorseCount, int Winner, int Variant);

public sealed class HorseRaceRenderJob : IRenderJob<HorseRaceRenderSpec>
{
    public const string RendererId = "horse-race";
    public const string RendererVersion = "2";

    public RenderKey Describe(HorseRaceRenderSpec spec)
    {
        Validate(spec);
        var identity = $"horses={spec.HorseCount};winner={spec.Winner};variant={spec.Variant}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
        return new RenderKey(RendererId, RendererVersion, hash, "gif", "image/gif");
    }

    public ValueTask<RenderOutput> RenderAsync(HorseRaceRenderSpec spec, CancellationToken ct)
    {
        Validate(spec);
        ct.ThrowIfCancellationRequested();
        var speeds = CreateSpeeds(spec);
        var (frames, height, width) = HorseRaceRenderer.DrawHorses(speeds);
        ct.ThrowIfCancellationRequested();
        var gif = GifEncoder.RenderFramesToGif(frames, width, height);
        return ValueTask.FromResult(RenderOutput.FromBytes(gif, "horses.gif"));
    }

    public static int EstimateFrameCount(HorseRaceRenderSpec spec) =>
        CreateSpeeds(spec).Max(static series => series.Length) + 90;

    private static double[][] CreateSpeeds(HorseRaceRenderSpec spec) =>
        SpeedGenerator.CreateSpeeds(
            spec.HorseCount,
            spec.Winner,
            $"{RendererId}:{RendererVersion}:{spec.HorseCount}:{spec.Winner}:{spec.Variant}");

    private static void Validate(HorseRaceRenderSpec spec)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(spec.HorseCount, 2);
        ArgumentOutOfRangeException.ThrowIfNegative(spec.Winner);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(spec.Winner, spec.HorseCount);
        ArgumentOutOfRangeException.ThrowIfNegative(spec.Variant);
    }
}
