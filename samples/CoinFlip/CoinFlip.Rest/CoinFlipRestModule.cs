using BotFramework.Rest;
using CoinFlip.Application;
using CoinFlip.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CoinFlip.Rest;

public sealed class CoinFlipRestModule : IRestRouteModule
{
    public string ModuleId => "coin-flip";

    public void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapRestGroup(ModuleId)
            .MapPost("/flip", FlipAsync)
            .WithName("CoinFlipFlip")
            .Produces<CoinFlipReply>()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }

    private static async Task<IResult> FlipAsync(
        RestRequestContext context,
        CoinFlipService service,
        CancellationToken ct)
    {
        var operationId = context.RequireIdempotencyKey();
        var reply = await service.ExecuteAsync(
            new CoinFlipCommand(context.Tenant, context.Scope, context.Player, operationId),
            Random.Shared.Next(),
            ct).ConfigureAwait(false);
        return Results.Ok(reply);
    }
}
