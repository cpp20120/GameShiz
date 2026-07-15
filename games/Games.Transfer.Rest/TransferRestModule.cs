using BotFramework.Rest;
using Games.Transfer.Application.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Games.Transfer.Rest;

public sealed record TransferRestRequest(long ToUserId, string RecipientDisplayName, int Amount);

public sealed class TransferRestModule : IRestRouteModule
{
    public string ModuleId => "transfer";

    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapRestGroup(ModuleId).MapPost("", TransferAsync).WithName("TransferCoins").WithSummary("Transfer coins to another player");

    private static async Task<IResult> TransferAsync(TransferRestRequest request, ITransferService service, RestRequestContext context,
        IOptions<RestFrameworkOptions> options, CancellationToken ct)
    {
        if (request.ToUserId <= 0) throw new RestBadRequestException("ToUserId must be positive.");
        RestCommandSupport.RequireText(request.RecipientDisplayName, nameof(request.RecipientDisplayName));
        RestCommandSupport.RequirePositive(request.Amount, nameof(request.Amount));
        var result = await service.TryTransferAsync(context.UserId, request.ToUserId, RestCommandSupport.ScopeId(context),
            context.DisplayName, request.RecipientDisplayName, request.Amount,
            RestCommandSupport.SourceId(context, options, "transfer", "send"), ct).ConfigureAwait(false);
        return Results.Ok(result);
    }
}

public static class TransferRestServiceCollectionExtensions
{
    public static IServiceCollection AddTransferRest(this IServiceCollection services) => services.AddRestRouteModule<TransferRestModule>();
}
