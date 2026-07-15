using BotFramework.Rest;
using Games.Pick.Application.Services;
using Games.Pick.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Games.Pick.Rest;

public sealed record PickRequest(int Amount, IReadOnlyList<string> Variants, IReadOnlyList<int> BackedIndices);
public sealed record PickLotteryOpenRequest(int Stake);
public sealed record PickDailyBuyRequest(int Count);

public sealed class PickRestModule : IRestRouteModule
{
    public string ModuleId => "pick";

    public void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapRestGroup(ModuleId);
        group.MapPost("", PickAsync).WithName("PickPlay");
        group.MapPost("/lottery", OpenLotteryAsync).WithName("PickLotteryOpen");
        group.MapPost("/lottery/join", JoinLotteryAsync).WithName("PickLotteryJoin");
        group.MapGet("/lottery", LotteryInfoAsync).WithName("PickLotteryInfo");
        group.MapDelete("/lottery", CancelLotteryAsync).WithName("PickLotteryCancel");
        group.MapPost("/daily", BuyDailyAsync).WithName("PickDailyBuy");
        group.MapGet("/daily", DailyInfoAsync).WithName("PickDailyInfo");
        group.MapGet("/daily/history", DailyHistoryAsync).WithName("PickDailyHistory");
        group.MapGet("/daily/schedule", DailyScheduleAsync).WithName("PickDailySchedule");
        group.MapPost("/chains/continue", ContinueChainAsync).WithName("PickContinueChain");
        group.MapPost("/chains/{chainId:guid}/claim", ClaimChainAsync).WithName("PickClaimChain");
    }

    private static async Task<IResult> PickAsync(PickRequest request, IPickClient client, RestRequestContext context,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        ValidatePick(request);
        return Results.Ok(await client.PickAsync(context.UserId, context.DisplayName, RestCommandSupport.ScopeId(context), request.Amount,
            request.Variants, request.BackedIndices, RestCommandSupport.SourceId(context, options, "pick", "play"), ct).ConfigureAwait(false));
    }

    private static async Task<IResult> OpenLotteryAsync(PickLotteryOpenRequest request, IPickClient client, RestRequestContext context,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        RestCommandSupport.RequirePositive(request.Stake, nameof(request.Stake));
        return Results.Ok(await client.OpenLotteryAsync(context.UserId, context.DisplayName, RestCommandSupport.ScopeId(context), request.Stake,
            RestCommandSupport.SourceId(context, options, "pick", "lottery-open"), ct).ConfigureAwait(false));
    }

    private static async Task<IResult> JoinLotteryAsync(IPickClient client, RestRequestContext context, IOptions<RestFrameworkOptions> options, CancellationToken ct) =>
        Results.Ok(await client.JoinLotteryAsync(context.UserId, context.DisplayName, RestCommandSupport.ScopeId(context),
            RestCommandSupport.SourceId(context, options, "pick", "lottery-join"), ct).ConfigureAwait(false));

    private static async Task<IResult> LotteryInfoAsync(IPickClient client, RestRequestContext context, CancellationToken ct)
    {
        var result = await client.LotteryInfoAsync(RestCommandSupport.ScopeId(context), ct).ConfigureAwait(false);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> CancelLotteryAsync(IPickClient client, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await client.CancelLotteryAsync(context.UserId, RestCommandSupport.ScopeId(context), ct).ConfigureAwait(false));

    private static async Task<IResult> BuyDailyAsync(PickDailyBuyRequest request, IPickClient client, RestRequestContext context,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        RestCommandSupport.RequirePositive(request.Count, nameof(request.Count));
        return Results.Ok(await client.BuyDailyAsync(context.UserId, context.DisplayName, RestCommandSupport.ScopeId(context), request.Count,
            RestCommandSupport.SourceId(context, options, "pick", "daily-buy"), ct).ConfigureAwait(false));
    }

    private static async Task<IResult> DailyInfoAsync(IPickClient client, RestRequestContext context, CancellationToken ct)
    {
        var result = await client.DailyInfoAsync(RestCommandSupport.ScopeId(context), context.UserId, ct).ConfigureAwait(false);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> DailyHistoryAsync(int? limit, IPickClient client, RestRequestContext context, CancellationToken ct)
    {
        var actualLimit = limit ?? 30;
        if (actualLimit is < 1 or > 100) throw new RestBadRequestException("limit must be between 1 and 100.");
        return Results.Ok(await client.DailyHistoryAsync(RestCommandSupport.ScopeId(context), actualLimit, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> DailyScheduleAsync(IPickClient client, CancellationToken ct) =>
        Results.Ok(await client.GetDailyScheduleAsync(ct).ConfigureAwait(false));

    private static async Task<IResult> ContinueChainAsync(PickChainState chain, IPickClient client, RestRequestContext context, CancellationToken ct)
    {
        if (chain.UserId != context.UserId || chain.ChatId != RestCommandSupport.ScopeId(context))
            throw new RestForbiddenException("The chain does not belong to the current player and scope.");
        return Results.Ok(await client.ContinueChainAsync(chain, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ClaimChainAsync(Guid chainId, IPickClient client, RestRequestContext context, CancellationToken ct)
    {
        var result = await client.ClaimChainAsync(chainId, ct).ConfigureAwait(false);
        return result is null || result.UserId != context.UserId ? Results.NotFound() : Results.Ok(result);
    }

    private static void ValidatePick(PickRequest request)
    {
        RestCommandSupport.RequirePositive(request.Amount, nameof(request.Amount));
        if (request.Variants is null || request.Variants.Count is < 2 or > 32)
            throw new RestBadRequestException("Variants must contain between 2 and 32 items.");
        if (request.Variants.Any(string.IsNullOrWhiteSpace) || request.Variants.Any(x => x.Length > 128))
            throw new RestBadRequestException("Each variant must be non-empty and at most 128 characters.");
        if (request.BackedIndices is null || request.BackedIndices.Count == 0 || request.BackedIndices.Any(x => x < 0 || x >= request.Variants.Count))
            throw new RestBadRequestException("BackedIndices must point to existing variants.");
    }
}

public static class PickRestServiceCollectionExtensions
{
    public static IServiceCollection AddPickRest(this IServiceCollection services) => services.AddRestRouteModule<PickRestModule>();
}
