using BotFramework.Rest;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SampleGame.Application;
using SampleGame.Contracts;
using SampleGame.Domain;

namespace SampleGame.Rest;

public sealed class SampleGameRestModule : IRestRouteModule
{
    public string ModuleId => "sample-game";

    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapRestGroup(ModuleId)
            .MapPost("/play", (RestRequestContext context, SampleGameService service, CancellationToken ct) =>
                Task.FromResult<IResult>(Results.Ok(service.Execute(
                    new SampleGameCommand(context.Tenant, context.Scope, context.Player, context.RequireIdempotencyKey()),
                    SampleGameState.Empty))))
            .WithName("SampleGamePlay")
            .Produces<SampleGameReply>()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
