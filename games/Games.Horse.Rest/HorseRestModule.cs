using BotFramework.Rest;
using Games.Horse.Application.Services;
using Games.Horse.Domain.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Games.Horse.Rest;

public sealed record HorseBetRequest(int HorseId, int Amount);

public sealed class HorseRestModule : IRestRouteModule
{
    public string ModuleId => "horse";

    public void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapRestGroup(ModuleId);
        group.MapGet("/info", InfoAsync).WithName("HorseInfo");
        group.MapGet("/result", ResultAsync).WithName("HorseResult");
        group.MapPost("/bet", BetAsync).WithName("HorseBet");
    }

    private static async Task<IResult> InfoAsync(IHorseService service, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await service.GetTodayInfoAsync(RestCommandSupport.ScopeId(context), ct).ConfigureAwait(false));

    private static async Task<IResult> ResultAsync(IHorseService service, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await service.GetTodayResultAsync(RestCommandSupport.ScopeId(context), ct).ConfigureAwait(false));

    private static async Task<IResult> BetAsync(HorseBetRequest request, IHorseService service, RestRequestContext context,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        RestCommandSupport.RequirePositive(request.HorseId, nameof(request.HorseId));
        RestCommandSupport.RequirePositive(request.Amount, nameof(request.Amount));
        var result = await service.PlaceBetAsync(context.UserId, context.DisplayName, RestCommandSupport.ScopeId(context), request.HorseId,
            request.Amount, RestCommandSupport.SourceId(context, options, "horse", "bet"), ct).ConfigureAwait(false);
        return Results.Ok(result);
    }
}

public static class HorseRestServiceCollectionExtensions
{
    public static IServiceCollection AddHorseRest(this IServiceCollection services) => services.AddRestRouteModule<HorseRestModule>();
}
