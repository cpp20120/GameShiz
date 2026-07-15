using Microsoft.Extensions.Options;
using BotFramework.Contracts.Tenancy;

namespace BotFramework.Rest;

public static class RestCommandSupport
{
    public static string OperationId(
        RestRequestContext context,
        IOptions<RestFrameworkOptions> options,
        string module,
        string action)
    {
        var key = options.Value.RequireIdempotencyKeyForCommands
            ? context.RequireIdempotencyKey()
            : context.IdempotencyKey ?? context.RequestId;
        return $"rest:{module}:{action}:{context.Tenant.Value}:{context.Scope.Value}:{context.Player.Value}:{key}";
    }

    public static int SourceId(
        RestRequestContext context,
        IOptions<RestFrameworkOptions> options,
        string module,
        string action) =>
        RestIdempotency.ToStableSourceId(OperationId(context, options, module, action));

    public static long ScopeId(RestRequestContext context, string name = "scopeId") =>
        long.TryParse(context.ScopeId, out var value)
            ? value
            : throw new RestBadRequestException($"{name} must be a numeric scope.");

    public static ScopeId Scope(RestRequestContext context) => context.Scope;

    public static TenantId Tenant(RestRequestContext context) => context.Tenant;

    public static void RequirePositive(int value, string name)
    {
        if (value <= 0)
            throw new RestBadRequestException($"{name} must be positive.");
    }

    public static void RequireText(string? value, string name, int maxLength = 256)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
            throw new RestBadRequestException($"{name} is required and must be at most {maxLength} characters.");
    }
}
