using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Games.PixelBattle.Infrastructure.Integrations;

public static class PixelBattleEndpointExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static WebApplication MapPixelBattle(this WebApplication app)
    {
        app.MapGet("/pixelbattle", () => Results.Redirect("/pixelbattle/index.html", permanent: false));
        app.MapGet("/pixelbattle/", () => Results.Redirect("/pixelbattle/index.html", permanent: false));

        var api = app.MapGroup("/pixelbattle/api");
        api.MapGet("/grid", async (IPixelBattleStore store, CancellationToken ct) =>
            Results.Json(await store.GetGridAsync(ct), JsonOptions));
        api.MapPost("/update", UpdateAsync);
        api.MapGet("/listen", ListenAsync);

        return app;
    }

    private static async Task<IResult> UpdateAsync(
        HttpContext context,
        JsonElement body,
        ITelegramWebAppInitDataValidator validator,
        IPixelBattleStore store,
        PixelBattleBroadcaster broadcaster,
        IAnalyticsService analytics)
    {
        if (!validator.TryValidate(context.Request.Headers["X-Telegram-Init-Data"], out var auth))
        {
            TrackUpdate(analytics, 0, "invalid_auth");
            return Results.Json("invalid init data", statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!TryReadUpdateBody(body, out var index, out var color))
        {
            TrackUpdate(analytics, auth.User.Id, "invalid_body");
            return Results.Json("invalid update", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!PixelBattleConstants.IsValidIndex(index))
        {
            TrackUpdate(analytics, auth.User.Id, "invalid_index");
            return Results.Json("invalid index", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!PixelBattleConstants.IsValidColor(color))
        {
            TrackUpdate(analytics, auth.User.Id, "invalid_color");
            return Results.Json("invalid color", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!await store.IsKnownUserAsync(auth.User.Id, context.RequestAborted))
        {
            TrackUpdate(analytics, auth.User.Id, "unknown_user");
            return Results.Json("unknown user", statusCode: StatusCodes.Status403Forbidden);
        }

        var update = await store.UpdateTileAsync(index, color, auth.User.Id, context.RequestAborted);
        broadcaster.Broadcast(update);
        TrackUpdate(analytics, auth.User.Id, "success", index, color, update.Versionstamp);

        return Results.Json(update.Versionstamp, JsonOptions);
    }

    private static void TrackUpdate(
        IAnalyticsService analytics, long userId, string outcome,
        int? index = null, string? color = null, string? versionstamp = null)
    {
        var tags = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["user_id"] = userId,
            ["outcome"] = outcome,
        };
        if (index is not null) tags["index"] = index.Value;
        if (color is not null) tags["color"] = color;
        if (versionstamp is not null) tags["versionstamp"] = versionstamp;
        analytics.Track("pixelbattle", "tile_updated", tags);
    }

    private static bool TryReadUpdateBody(JsonElement body, out int index, out string color)
    {
        index = default;
        color = "";

        if (body.ValueKind != JsonValueKind.Array || body.GetArrayLength() != 2)
            return false;

        var indexElement = body[0];
        var colorElement = body[1];
        if (indexElement.ValueKind != JsonValueKind.Number ||
            !indexElement.TryGetInt32(out index) ||
            colorElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        color = colorElement.GetString() ?? "";
        return true;
    }

    private static async Task ListenAsync(
        HttpContext context,
        IPixelBattleStore store,
        PixelBattleBroadcaster broadcaster,
        IOptions<PixelBattleOptions> options)
    {
        var ct = context.RequestAborted;
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";
        context.Response.ContentType = "text/event-stream; charset=utf-8";

        await context.Response.WriteAsync("retry: 1000\n\n", ct);
        await WriteFullGridAsync(context, store, ct);

        using var subscription = broadcaster.Subscribe();
        using var timer = new PeriodicTimer(options.Value.FullUpdateInterval);
        var reader = subscription.Reader;

        var updateTask = reader.WaitToReadAsync(ct).AsTask();
        var timerTask = timer.WaitForNextTickAsync(ct).AsTask();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var completed = await Task.WhenAny(updateTask, timerTask);
                if (completed == updateTask)
                {
                    if (!await updateTask)
                        break;

                    while (reader.TryRead(out var update))
                        await WriteUpdatesAsync(context, [update], ct);

                    updateTask = reader.WaitToReadAsync(ct).AsTask();
                    continue;
                }

                if (!await timerTask)
                    break;

                await WriteFullGridAsync(context, store, ct);
                timerTask = timer.WaitForNextTickAsync(ct).AsTask();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private static async Task WriteFullGridAsync(HttpContext context, IPixelBattleStore store, CancellationToken ct)
    {
        var grid = await store.GetGridAsync(ct);
        var updates = new PixelBattleUpdate[grid.Tiles.Length];
        for (var i = 0; i < grid.Tiles.Length; i++)
            updates[i] = new PixelBattleUpdate(i, grid.Tiles[i], grid.Versionstamps[i]);

        await WriteUpdatesAsync(context, updates, ct);
    }

    private static async Task WriteUpdatesAsync(
        HttpContext context,
        IReadOnlyCollection<PixelBattleUpdate> updates,
        CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(updates, JsonOptions);
        await context.Response.WriteAsync($"data: {json}\n\n", ct);
        await context.Response.Body.FlushAsync(ct);
    }
}
