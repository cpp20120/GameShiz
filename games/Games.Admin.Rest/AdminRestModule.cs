using BotFramework.Rest;
using Games.Admin.Application.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Admin.Rest;

public sealed record AdminPayRequest(long TargetUserId, long BalanceScopeId, int Amount);
public sealed record AdminRenameRequest(string OldName, string NewName);

public sealed class AdminRestModule : IRestRouteModule
{
    public string ModuleId => "admin";

    public void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapRestGroup(ModuleId);
        group.MapPost("/sync", SyncAsync).WithName("AdminSync");
        group.MapGet("/users/{targetUserId:long}", UserAsync).WithName("AdminUser");
        group.MapPost("/pay", PayAsync).WithName("AdminPay");
        group.MapPost("/clear-bets", ClearBetsAsync).WithName("AdminClearBets");
        group.MapPost("/rename", RenameAsync).WithName("AdminRename");
    }

    private static async Task<IResult> SyncAsync(IAdminService service, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(new { Synced = await service.UserSyncAsync(context.UserId, ct).ConfigureAwait(false) });

    private static async Task<IResult> UserAsync(long targetUserId, long? balanceScopeId, IAdminService service, RestRequestContext context, CancellationToken ct)
    {
        if (targetUserId <= 0) throw new RestBadRequestException("targetUserId must be positive.");
        var scope = balanceScopeId ?? RestCommandSupport.ScopeId(context);
        var result = await service.GetUserAsync(targetUserId, scope, ct).ConfigureAwait(false);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> PayAsync(AdminPayRequest request, IAdminService service, RestRequestContext context, CancellationToken ct)
    {
        if (request.TargetUserId <= 0 || request.BalanceScopeId == 0) throw new RestBadRequestException("Target user and scope are required.");
        RestCommandSupport.RequirePositive(request.Amount, nameof(request.Amount));
        var result = await service.PayAsync(context.UserId, request.TargetUserId, request.BalanceScopeId, request.Amount, ct).ConfigureAwait(false);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> ClearBetsAsync(IAdminService service, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await service.ClearChatBetsAsync(context.UserId, RestCommandSupport.ScopeId(context), ct).ConfigureAwait(false));

    private static async Task<IResult> RenameAsync(AdminRenameRequest request, IAdminService service, RestRequestContext context, CancellationToken ct)
    {
        RestCommandSupport.RequireText(request.OldName, nameof(request.OldName));
        RestCommandSupport.RequireText(request.NewName, nameof(request.NewName));
        return Results.Ok(await service.RenameAsync(context.UserId, request.OldName, request.NewName, ct).ConfigureAwait(false));
    }
}

public static class AdminRestServiceCollectionExtensions
{
    public static IServiceCollection AddAdminRest(this IServiceCollection services) => services.AddRestRouteModule<AdminRestModule>();
}
