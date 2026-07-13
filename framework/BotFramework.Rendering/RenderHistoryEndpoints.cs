using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BotFramework.Rendering;

public static class RenderHistoryEndpoints
{
    public static IEndpointRouteBuilder MapRenderHistory(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/admin/render-history/{gameId}/{aggregateId}", async (
            string gameId,
            string aggregateId,
            int? take,
            IRenderHistory history,
            CancellationToken ct) =>
        {
            var result = new List<object>();
            await foreach (var entry in history.ListAsync(gameId, aggregateId, Math.Clamp(take ?? 50, 1, 200), ct))
            {
                var key = entry.ArtifactKey;
                var artifactUrl = $"/admin/render-artifact/{Uri.EscapeDataString(key.RendererId)}/"
                    + $"{Uri.EscapeDataString(key.RendererVersion)}/{Uri.EscapeDataString(key.ContentHash)}."
                    + Uri.EscapeDataString(key.Extension);
                result.Add(new
                {
                    entry.GameId,
                    entry.AggregateId,
                    entry.MatchId,
                    entry.CreatedAt,
                    entry.Metadata,
                    artifactUrl,
                });
            }
            return Results.Ok(result);
        });

        endpoints.MapGet("/admin/render-artifact/{rendererId}/{version}/{file}", async (
            string rendererId,
            string version,
            string file,
            IRenderArtifactStore store,
            CancellationToken ct) =>
        {
            var separator = file.LastIndexOf('.');
            if (separator <= 0 || separator == file.Length - 1) return Results.BadRequest();
            var hash = file[..separator];
            var extension = file[(separator + 1)..].ToLowerInvariant();
            var contentType = extension switch
            {
                "gif" => "image/gif",
                "png" => "image/png",
                "jpg" or "jpeg" => "image/jpeg",
                "webp" => "image/webp",
                _ => "application/octet-stream",
            };
            var artifact = await store.FindAsync(
                new RenderKey(rendererId, version, hash, extension, contentType), ct);
            return artifact is null
                ? Results.NotFound()
                : Results.File(artifact.Content, artifact.Key.ContentType, artifact.FileName);
        });

        return endpoints;
    }
}
