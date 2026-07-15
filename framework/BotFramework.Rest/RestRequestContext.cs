using System.Security.Claims;
using System.Security.Cryptography;
using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;
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
    /// <summary>Opaque tenant identity from the canonical REST route.</summary>
    public TenantId Tenant { get; init; }

    /// <summary>Opaque scope identity from the canonical REST route.</summary>
    public ScopeId Scope { get; init; }

    /// <summary>Opaque player identity from the JWT subject.</summary>
    public PlayerId Player { get; init; }

    public RequestId RequestIdentifier { get; init; }

    public RequestId CorrelationIdentifier { get; init; }

    public TenantContext TenantContext { get; init; } = null!;

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
        if (string.IsNullOrWhiteSpace(subject))
            throw new RestUnauthorizedException("JWT sub is required.");

        var tenantId = httpContext.Request.RouteValues.TryGetValue("tenantId", out var tenantValue)
            ? Convert.ToString(tenantValue, System.Globalization.CultureInfo.InvariantCulture)
            : null;
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new RestBadRequestException("The tenantId route value is required.");

        var scopeId = httpContext.Request.RouteValues.TryGetValue("scopeId", out var value)
            ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
            : null;
        if (string.IsNullOrWhiteSpace(scopeId))
            throw new RestBadRequestException("The scopeId route value is required.");

        var typedTenant = TenantId.Create(tenantId);
        var typedScope = ScopeId.Create(scopeId);
        var typedPlayer = PlayerId.Create(subject);
        ValidateTenant(principal, tenantId);
        ValidateScope(principal, scopeId);

        var correlationId = GetCorrelationId(httpContext);
        var requestId = GetRequestId(httpContext, correlationId);
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

        var context = new RestRequestContext(
            subject,
            long.TryParse(subject, out var numericUserId) ? numericUserId : 0,
            displayName,
            scopeId,
            correlationId,
            idempotencyKey,
            baggage)
        {
            Tenant = typedTenant,
            Scope = typedScope,
            Player = typedPlayer,
            RequestIdentifier = RequestId.Create(requestId),
            CorrelationIdentifier = RequestId.Create(correlationId),
        };
        context = context with
        {
            TenantContext = TenantContext.Create(
                typedTenant,
                typedScope,
                typedPlayer,
                BotChannel.Rest,
                context.RequestIdentifier,
                context.CorrelationIdentifier),
        };
        return context;
    }

    private void ValidateTenant(ClaimsPrincipal principal, string tenantId)
    {
        var claimedTenants = principal.Claims
            .Where(claim => claim.Type is "tenant_id" or "tenantId")
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (claimedTenants.Length == 0)
        {
            if (options.Value.RequireTenantClaim)
                throw new RestForbiddenException("The token does not grant access to this tenant.", "tenant_access_denied");
            return;
        }

        if (!claimedTenants.Contains(tenantId, StringComparer.Ordinal))
            throw new RestForbiddenException("The token does not grant access to this tenant.", "tenant_access_denied");
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

    private static string GetRequestId(HttpContext context, string fallback)
    {
        var supplied = context.Request.Headers["X-Request-ID"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(supplied) && supplied.Length <= 128 && !supplied.Any(char.IsControl))
            return supplied;

        return fallback;
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
