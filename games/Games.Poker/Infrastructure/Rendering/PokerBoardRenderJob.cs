using System.Security.Cryptography;
using System.Text.Json;
using BotFramework.Rendering;

namespace Games.Poker.Infrastructure.Rendering;

public sealed record PokerBoardRenderSpec(TableSnapshot Snapshot, string CultureCode = "ru");

public sealed class PokerBoardRenderJob(ILocalizer localizer) : IRenderJob<PokerBoardRenderSpec>
{
    public RenderKey Describe(PokerBoardRenderSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var canonical = JsonSerializer.SerializeToUtf8Bytes(new
        {
            culture = spec.CultureCode,
            snapshot = spec.Snapshot,
        });
        var hash = Convert.ToHexString(SHA256.HashData(canonical)).ToLowerInvariant();
        return new RenderKey("poker-board", "2", hash, "png", "image/png");
    }

    public ValueTask<RenderOutput> RenderAsync(PokerBoardRenderSpec spec, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var bytes = PokerBoardRenderer.Render(spec.Snapshot, new CultureLocalizer(localizer, spec.CultureCode));
        return ValueTask.FromResult(RenderOutput.FromBytes(bytes, "poker-board.png"));
    }

    private sealed class CultureLocalizer(ILocalizer inner, string cultureCode) : ILocalizer
    {
        public string Get(string moduleId, string key, string cultureCodeOverride = "ru") =>
            inner.Get(moduleId, key, cultureCode);

        public string GetPlural(string moduleId, string key, int count, string cultureCodeOverride = "ru") =>
            inner.GetPlural(moduleId, key, count, cultureCode);
    }
}
