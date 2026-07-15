using BotFramework.Rest;
using Games.Blackjack.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Games.Blackjack.Rest;

public sealed record BlackjackStartRequest(int Bet);

public sealed class BlackjackRestModule : IRestRouteModule
{
    public string ModuleId => "blackjack";

    public void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapRestGroup(ModuleId);
        group.MapGet("/state", StateAsync).WithName("BlackjackState");
        group.MapPost("/start", StartAsync).WithName("BlackjackStart");
        group.MapPost("/hit", HitAsync).WithName("BlackjackHit");
        group.MapPost("/stand", StandAsync).WithName("BlackjackStand");
        group.MapPost("/double", DoubleAsync).WithName("BlackjackDouble");
    }

    private static async Task<IResult> StateAsync(IBlackjackClient client, RestRequestContext context, CancellationToken ct)
    {
        var state = await client.GetStateAsync(context.UserId, ct).ConfigureAwait(false);
        return state.Snapshot is null ? Results.NotFound() : Results.Ok(state);
    }

    private static async Task<IResult> StartAsync(
        BlackjackStartRequest request, IBlackjackClient client, RestRequestContext context,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        RestCommandSupport.RequirePositive(request.Bet, nameof(request.Bet));
        var result = await client.StartAsync(context.UserId, context.DisplayName, RestCommandSupport.ScopeId(context), request.Bet,
            RestCommandSupport.OperationId(context, options, "blackjack", "start"), ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HitAsync(IBlackjackClient client, RestRequestContext context, IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        RequireCommandKey(context, options);
        return Results.Ok(await client.HitAsync(context.UserId, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> StandAsync(IBlackjackClient client, RestRequestContext context, IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        RequireCommandKey(context, options);
        return Results.Ok(await client.StandAsync(context.UserId, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> DoubleAsync(IBlackjackClient client, RestRequestContext context, IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        RequireCommandKey(context, options);
        return Results.Ok(await client.DoubleAsync(context.UserId, ct).ConfigureAwait(false));
    }

    private static void RequireCommandKey(RestRequestContext context, IOptions<RestFrameworkOptions> options)
    {
        if (options.Value.RequireIdempotencyKeyForCommands) _ = context.RequireIdempotencyKey();
    }
}

public static class BlackjackRestServiceCollectionExtensions
{
    public static IServiceCollection AddBlackjackRest(this IServiceCollection services) => services.AddRestRouteModule<BlackjackRestModule>();
}
