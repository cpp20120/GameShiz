using BotFramework.Rest;
using Games.Poker.Application.Services;
using Games.Poker.Domain.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Games.Poker.Rest;

public sealed record PokerActionRequest(string Verb, int Amount = 0);

public sealed class PokerRestModule : IRestRouteModule
{
    public string ModuleId => "poker";

    public void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapRestGroup(ModuleId);
        group.MapGet("/tables/me", FindMyTableAsync)
            .WithName("PokerFindMyTable")
            .WithSummary("Read the authenticated player's current table")
            .Produces<TableSnapshot>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/tables", CreateTableAsync)
            .WithName("PokerCreateTable")
            .WithSummary("Create a poker table")
            .Produces<CreateResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPost("/tables/{tableId}/join", JoinTableAsync)
            .WithName("PokerJoinTable")
            .Produces<JoinResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPost("/tables/{tableId}/start", StartHandAsync)
            .WithName("PokerStartHand")
            .Produces<StartResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPost("/tables/{tableId}/actions", ApplyActionAsync)
            .WithName("PokerApplyAction")
            .Produces<ActionResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapDelete("/tables/{tableId}/players/me", LeaveTableAsync)
            .WithName("PokerLeaveTable")
            .Produces<LeaveResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> FindMyTableAsync(
        RestRequestContext context,
        IPokerService service,
        CancellationToken cancellationToken)
    {
        var scopeId = ParseScope(context);
        var (snapshot, seat) = await service.FindMyTableAsync(context.UserId, scopeId, cancellationToken)
            .ConfigureAwait(false);
        if (snapshot is null || seat is null)
            throw new RestNotFoundException("The authenticated player is not seated at a table in this scope.");
        return Results.Ok(snapshot);
    }

    private static async Task<IResult> CreateTableAsync(
        RestRequestContext context,
        IPokerService service,
        IOptions<RestFrameworkOptions> options,
        CancellationToken cancellationToken)
    {
        var operationId = RequireOperationId(context, options, "create");
        var result = await service.CreateTableAsync(
            context.UserId, context.DisplayName, ParseScope(context), operationId, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(result.Error);
        return Results.Created($"tables/{result.InviteCode}", result);
    }

    private static async Task<IResult> JoinTableAsync(
        string tableId,
        RestRequestContext context,
        IPokerService service,
        IOptions<RestFrameworkOptions> options,
        CancellationToken cancellationToken)
    {
        var operationId = RequireOperationId(context, options, "join");
        var result = await service.JoinTableAsync(
            context.UserId, context.DisplayName, ParseScope(context), RequireTableId(tableId), operationId, cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(result.Error);
        return Results.Ok(result);
    }

    private static async Task<IResult> StartHandAsync(
        string tableId,
        RestRequestContext context,
        IPokerService service,
        IOptions<RestFrameworkOptions> options,
        CancellationToken cancellationToken)
    {
        var operationId = RequireOperationId(context, options, "start");
        var result = await service.StartHandAsync(
            context.UserId, ParseScope(context), operationId, cancellationToken).ConfigureAwait(false);
        EnsureTable(result.Snapshot, tableId);
        EnsureSuccess(result.Error);
        return Results.Ok(result);
    }

    private static async Task<IResult> ApplyActionAsync(
        string tableId,
        PokerActionRequest request,
        RestRequestContext context,
        IPokerService service,
        IOptions<RestFrameworkOptions> options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Verb) || request.Verb.Length > 16)
            throw new RestBadRequestException("Verb is required and must be at most 16 characters.");
        var operationId = RequireOperationId(context, options, "action");
        var result = await service.ApplyPlayerActionAsync(
            context.UserId, ParseScope(context), request.Verb.ToLowerInvariant(), request.Amount,
            operationId, cancellationToken).ConfigureAwait(false);
        EnsureTable(result.Snapshot, tableId);
        EnsureSuccess(result.Error);
        return Results.Ok(result);
    }

    private static async Task<IResult> LeaveTableAsync(
        string tableId,
        RestRequestContext context,
        IPokerService service,
        IOptions<RestFrameworkOptions> options,
        CancellationToken cancellationToken)
    {
        var operationId = RequireOperationId(context, options, "leave");
        var result = await service.LeaveTableAsync(
            context.UserId, ParseScope(context), operationId, cancellationToken).ConfigureAwait(false);
        EnsureTable(result.Snapshot, tableId, allowMissing: result.TableClosed);
        EnsureSuccess(result.Error);
        return Results.Ok(result);
    }

    private static string RequireOperationId(RestRequestContext context, IOptions<RestFrameworkOptions> options, string action)
    {
        var key = options.Value.RequireIdempotencyKeyForCommands
            ? context.RequireIdempotencyKey()
            : context.IdempotencyKey ?? context.RequestId;
        return $"rest:poker:{action}:{context.ScopeId}:{context.UserId}:{key}";
    }

    private static long ParseScope(RestRequestContext context) =>
        long.TryParse(context.ScopeId, out var value)
            ? value
            : throw new RestBadRequestException("scopeId must be a numeric poker scope.");

    private static string RequireTableId(string tableId) =>
        string.IsNullOrWhiteSpace(tableId) || tableId.Length > 32
            ? throw new RestBadRequestException("tableId is required and must be at most 32 characters.")
            : tableId.ToUpperInvariant();

    private static void EnsureTable(TableSnapshot? snapshot, string tableId, bool allowMissing = false)
    {
        if (snapshot is null)
        {
            if (allowMissing) return;
            throw new RestNotFoundException("The requested poker table was not found in this scope.");
        }
        if (!string.Equals(snapshot.Table.InviteCode, RequireTableId(tableId), StringComparison.Ordinal))
            throw new RestNotFoundException("The requested poker table was not found in this scope.");
    }

    private static void EnsureSuccess(PokerError error)
    {
        if (error == PokerError.None) return;
        var (status, detail) = error switch
        {
            PokerError.NoTable or PokerError.TableNotFound => (404, "The requested poker table was not found."),
            PokerError.InvalidAction => (400, "The poker action is invalid."),
            _ => (409, $"The poker operation was rejected: {error}."),
        };
        throw status switch
        {
            400 => new RestBadRequestException(detail),
            404 => new RestNotFoundException(detail),
            _ => new RestConflictException(detail),
        };
    }
}

public static class PokerRestServiceCollectionExtensions
{
    public static IServiceCollection AddPokerRest(this IServiceCollection services) =>
        services.AddRestRouteModule<PokerRestModule>();
}
