namespace BotFramework.Rendering;

public interface IRenderJob<in TSpec>
{
    RenderKey Describe(TSpec spec);

    ValueTask<RenderOutput> RenderAsync(TSpec spec, CancellationToken ct);
}
