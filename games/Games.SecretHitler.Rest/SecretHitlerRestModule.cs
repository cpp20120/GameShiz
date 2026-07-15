using BotFramework.Rest;
using Games.SecretHitler.Application.Services;
using Games.SecretHitler.Domain.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.SecretHitler.Rest;

public sealed record SecretHitlerCreateRequest(long? PlayerChatId);
public sealed record SecretHitlerJoinRequest(string Code, long? PlayerChatId);
public sealed record SecretHitlerValueRequest(int Value);
public sealed record SecretHitlerVoteRequest(ShVote Vote);

public sealed class SecretHitlerRestModule : IRestRouteModule
{
    public string ModuleId => "secrethitler";

    public void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapRestGroup(ModuleId);
        group.MapGet("/game", FindAsync).WithName("SecretHitlerFindGame");
        group.MapPost("/game", CreateAsync).WithName("SecretHitlerCreateGame");
        group.MapPost("/game/join", JoinAsync).WithName("SecretHitlerJoinGame");
        group.MapPost("/game/start", StartAsync).WithName("SecretHitlerStartGame");
        group.MapPost("/game/nominate", NominateAsync).WithName("SecretHitlerNominate");
        group.MapPost("/game/vote", VoteAsync).WithName("SecretHitlerVote");
        group.MapPost("/game/discard", DiscardAsync).WithName("SecretHitlerDiscard");
        group.MapPost("/game/enact", EnactAsync).WithName("SecretHitlerEnact");
        group.MapDelete("/game", LeaveAsync).WithName("SecretHitlerLeave");
    }

    private static async Task<IResult> FindAsync(ISecretHitlerService service, RestRequestContext context, CancellationToken ct)
    {
        var result = await service.FindMyGameAsync(context.UserId, ct).ConfigureAwait(false);
        return result.Snapshot is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> CreateAsync(SecretHitlerCreateRequest request, ISecretHitlerService service, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await service.CreateGameAsync(context.UserId, context.DisplayName, RestCommandSupport.ScopeId(context),
            request.PlayerChatId ?? RestCommandSupport.ScopeId(context), ct).ConfigureAwait(false));

    private static async Task<IResult> JoinAsync(SecretHitlerJoinRequest request, ISecretHitlerService service, RestRequestContext context, CancellationToken ct)
    {
        RestCommandSupport.RequireText(request.Code, nameof(request.Code), 64);
        return Results.Ok(await service.JoinGameAsync(context.UserId, context.DisplayName, request.PlayerChatId ?? RestCommandSupport.ScopeId(context), request.Code, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> StartAsync(ISecretHitlerService service, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await service.StartGameAsync(context.UserId, ct).ConfigureAwait(false));

    private static async Task<IResult> NominateAsync(SecretHitlerValueRequest request, ISecretHitlerService service, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await service.NominateAsync(context.UserId, request.Value, ct).ConfigureAwait(false));

    private static async Task<IResult> VoteAsync(SecretHitlerVoteRequest request, ISecretHitlerService service, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await service.VoteAsync(context.UserId, request.Vote, ct).ConfigureAwait(false));

    private static async Task<IResult> DiscardAsync(SecretHitlerValueRequest request, ISecretHitlerService service, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await service.PresidentDiscardAsync(context.UserId, request.Value, ct).ConfigureAwait(false));

    private static async Task<IResult> EnactAsync(SecretHitlerValueRequest request, ISecretHitlerService service, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await service.ChancellorEnactAsync(context.UserId, request.Value, ct).ConfigureAwait(false));

    private static async Task<IResult> LeaveAsync(ISecretHitlerService service, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await service.LeaveAsync(context.UserId, ct).ConfigureAwait(false));
}

public static class SecretHitlerRestServiceCollectionExtensions
{
    public static IServiceCollection AddSecretHitlerRest(this IServiceCollection services) => services.AddRestRouteModule<SecretHitlerRestModule>();
}
