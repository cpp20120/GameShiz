using BotFramework.Rest;
using Games.Redeem.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Redeem.Rest;

public sealed record RedeemBeginRequest(string Code);
public sealed record RedeemVerifyRequest(Guid CodeGuid, int ChosenId);
public sealed record RedeemCompleteRequest(Guid CodeGuid);
public sealed record RedeemIssueRequest(string? FreeSpinGameId);

public sealed class RedeemRestModule : IRestRouteModule
{
    public string ModuleId => "redeem";

    public void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapRestGroup(ModuleId);
        group.MapPost("/begin", BeginAsync).WithName("RedeemBegin");
        group.MapPost("/verify", VerifyAsync).WithName("RedeemVerify");
        group.MapPost("/complete", CompleteAsync).WithName("RedeemComplete");
        group.MapPost("/issue", IssueAsync).WithName("RedeemIssue");
    }

    private static async Task<IResult> BeginAsync(RedeemBeginRequest request, IRedeemClient client, RestRequestContext context, CancellationToken ct)
    {
        RestCommandSupport.RequireText(request.Code, nameof(request.Code), 256);
        return Results.Ok(await client.BeginAsync(context.UserId, RestCommandSupport.ScopeId(context), context.DisplayName, request.Code, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> VerifyAsync(RedeemVerifyRequest request, IRedeemClient client, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await client.VerifyCaptchaAsync(context.UserId, request.CodeGuid, request.ChosenId, ct).ConfigureAwait(false));

    private static async Task<IResult> CompleteAsync(RedeemCompleteRequest request, IRedeemClient client, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await client.CompleteAsync(context.UserId, RestCommandSupport.ScopeId(context), request.CodeGuid, ct).ConfigureAwait(false));

    private static async Task<IResult> IssueAsync(RedeemIssueRequest request, IRedeemClient client, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await client.IssueAdminCodeAsync(context.UserId, request.FreeSpinGameId, ct).ConfigureAwait(false));
}

public static class RedeemRestServiceCollectionExtensions
{
    public static IServiceCollection AddRedeemRest(this IServiceCollection services) => services.AddRestRouteModule<RedeemRestModule>();
}
