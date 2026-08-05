using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BotFramework.Rendering;

public interface IRenderQueue
{
    ValueTask<RenderedArtifact> GetOrRenderAsync<TSpec>(
        TSpec spec,
        RenderPriority priority = RenderPriority.Interactive,
        CancellationToken ct = default);

    Task PrewarmAsync<TSpec>(IEnumerable<TSpec> specs, CancellationToken ct = default);
}
