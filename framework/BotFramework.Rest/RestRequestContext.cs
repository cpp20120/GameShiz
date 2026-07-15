using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BotFramework.Rest;

public sealed record RestRequestContext(
    string Subject,
    long UserId,
    string DisplayName,
    string ScopeId,
    string CorrelationId,
    string? IdempotencyKey,
    IReadOnlyDictionary<string, string> Baggage)
{
    public string RequestId => IdempotencyKey ?? CorrelationId;

    public string RequireIdempotencyKey()
    {
        if (string.IsNullOrWhiteSpace(IdempotencyKey))
            throw new RestBadRequestException("The Idempotency-Key header is required for state-changing requests.");

        return IdempotencyKey;
    }
}

public static class RestHttpContextExtensions
{
    public static RestRequestContext GetRestRequestContext(this HttpContext context) =>
        context.RequestServices.GetRequiredService<RestRequestContext>();
}

public static class RestIdempotency
{
    /// <summary>
    /// Legacy game contracts use an integer Telegram message id as part of the
    /// command id. This keeps arbitrary HTTP idempotency keys stable while the
    /// request still goes through the exact same atomic command path.
    /// </summary>
    public static int ToStableSourceId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        var result = BitConverter.ToInt32(hash, 0) & int.MaxValue;
        return result == 0 ? 1 : result;
    }
}

internal sealed class RestRequestContextFactory(IOptions<RestFrameworkOptions> options)
{
    public RestRequestContext Create(HttpContext httpContext)
    {
        var principal = httpContext.User;
        if (principal.Identity?.IsAuthenticated != true)
            throw new RestUnauthorizedException("A valid bearer token is required.");

        var subject = principal.FindFirstValue("sub")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject) || !long.TryParse(subject, out var userId))
            throw new RestUnauthorizedException("JWT sub must contain a numeric user id.");

        var scopeId = httpContext.Request.RouteValues.TryGetValue("scopeId", out var value)
            ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
            : null;
        if (string.IsNullOrWhiteSpace(scopeId))
            throw new RestBadRequestException("The scopeId route value is required.");

        ValidateScope(principal, scopeId);

        var correlationId = GetCorrelationId(httpContext);
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (idempotencyKey is not null)
        {
            if (idempotencyKey.Length is 0 or > 256 || idempotencyKey.Any(char.IsControl))
                throw new RestBadRequestException("Idempotency-Key must contain 1 to 256 printable characters.");
        }

        var displayName = principal.FindFirstValue("name")
            ?? principal.FindFirstValue("preferred_username")
            ?? principal.FindFirstValue(ClaimTypes.Name)
            ?? subject;
        var baggage = httpContext.Request.Headers
            .Where(header => header.Key.StartsWith("baggage-", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(header => header.Key, header => header.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        return new RestRequestContext(subject, userId, displayName, scopeId, correlationId, idempotencyKey, baggage);
    }

    private void ValidateScope(ClaimsPrincipal principal, string scopeId)
    {
        var claimedScopes = principal.Claims
            .Where(claim => claim.Type is "scope_id" or "scopeId" or "chat_id" or "chatId")
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (claimedScopes.Length == 0)
        {
            if (options.Value.RequireScopeClaim)
                throw new RestForbiddenException("The token does not grant access to this scope.");
            return;
        }

        if (!claimedScopes.Contains(scopeId, StringComparer.Ordinal))
            throw new RestForbiddenException("The token does not grant access to this scope.");
    }

    private static string GetCorrelationId(HttpContext context)
    {
        var supplied = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(supplied) && supplied.Length <= 128 && !supplied.Any(char.IsControl))
            return supplied;

        return context.TraceIdentifier;
    }
}

internal sealed class RestRequestContextEndpointFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        _ = context.HttpContext.GetRestRequestContext();
        return next(context);
    }
}
