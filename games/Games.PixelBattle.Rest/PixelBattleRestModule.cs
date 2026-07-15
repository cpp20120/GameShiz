using BotFramework.Rest;
using Games.PixelBattle.Contracts;
using Games.PixelBattle.Domain.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.PixelBattle.Rest;

public sealed record PixelUpdateRequest(int Index, string Color);

public sealed class PixelBattleRestModule : IRestRouteModule
{
    public string ModuleId => "pixelbattle";

    public void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapRestGroup(ModuleId);
        group.MapGet("/grid", GridAsync).WithName("PixelBattleGrid");
        group.MapPost("/pixels", UpdateAsync).WithName("PixelBattleUpdate");
    }

    private static async Task<IResult> GridAsync(IPixelBattleService service, CancellationToken ct) =>
        Results.Ok(await service.GetGridAsync(ct).ConfigureAwait(false));

    private static async Task<IResult> UpdateAsync(PixelUpdateRequest request, IPixelBattleService service, RestRequestContext context, CancellationToken ct)
    {
        if (!PixelBattleConstants.IsValidIndex(request.Index) || !PixelBattleConstants.IsValidColor(request.Color))
            throw new RestBadRequestException("The pixel index or color is invalid.");
        return Results.Ok(await service.UpdateAsync(context.UserId, request.Index, request.Color, ct).ConfigureAwait(false));
    }
}

public static class PixelBattleRestServiceCollectionExtensions
{
    public static IServiceCollection AddPixelBattleRest(this IServiceCollection services) => services.AddRestRouteModule<PixelBattleRestModule>();
}
