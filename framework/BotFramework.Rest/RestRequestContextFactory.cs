using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Claims;
using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BotFramework.Rest;

internal sealed class RestRequestContextFactory(IOptions<RestFrameworkOptions> options)
{
    private static readonly IReadOnlyDictionary<string, string> EmptyBaggage =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

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
        if (idempotencyKey is not null && (idempotencyKey.Length is 0 or > 256 || idempotencyKey.Any(char.IsControl))) throw new RestBadRequestException("Idempotency-Key must contain 1 to 256 printable characters.");

        var displayName = principal.FindFirstValue("name")
                          ?? principal.FindFirstValue("preferred_username")
                          ?? principal.FindFirstValue(ClaimTypes.Name)
                          ?? subject;
        var baggage = ReadBaggage(httpContext.Request.Headers);

        var requestIdentifier = RequestId.Create(requestId);
        var correlationIdentifier = RequestId.Create(correlationId);
        var tenantContext = TenantContext.Create(
            typedTenant,
            typedScope,
            typedPlayer,
            BotChannel.Rest,
            requestIdentifier,
            correlationIdentifier);

        return new RestRequestContext(
            subject,
            long.TryParse(subject, CultureInfo.InvariantCulture, out var numericUserId) ? numericUserId : 0,
            displayName,
            scopeId,
            correlationId,
            idempotencyKey,
            baggage)
        {
            Tenant = typedTenant,
            Scope = typedScope,
            Player = typedPlayer,
            RequestIdentifier = requestIdentifier,
            CorrelationIdentifier = correlationIdentifier,
            TenantContext = tenantContext,
        };
    }

    private void ValidateTenant(ClaimsPrincipal principal, string tenantId)
    {
        if (HasMatchingClaim(principal, tenantId, isTenant: true, out var hasClaim))
            return;
        if (!hasClaim && !options.Value.RequireTenantClaim)
            return;

        throw new RestForbiddenException(
            "The token does not grant access to this tenant.",
            "tenant_access_denied");
    }

    private void ValidateScope(ClaimsPrincipal principal, string scopeId)
    {
        if (HasMatchingClaim(principal, scopeId, isTenant: false, out var hasClaim))
            return;
        if (!hasClaim && !options.Value.RequireScopeClaim)
            return;

        throw new RestForbiddenException("The token does not grant access to this scope.");
    }

    private static bool HasMatchingClaim(
        ClaimsPrincipal principal,
        string expectedValue,
        bool isTenant,
        out bool hasClaim)
    {
        hasClaim = false;
        foreach (var claim in principal.Claims)
        {
            var isRelevant = isTenant
                ? claim.Type is "tenant_id" or "tenantId"
                : claim.Type is "scope_id" or "scopeId" or "chat_id" or "chatId";
            if (!isRelevant || string.IsNullOrWhiteSpace(claim.Value))
                continue;

            hasClaim = true;
            if (string.Equals(claim.Value, expectedValue, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static IReadOnlyDictionary<string, string> ReadBaggage(IHeaderDictionary headers)
    {
        Dictionary<string, string>? baggage = null;
        foreach (var header in headers)
        {
            if (!header.Key.StartsWith("baggage-", StringComparison.OrdinalIgnoreCase))
                continue;

            baggage ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            baggage[header.Key] = header.Value.ToString();
        }

        return baggage ?? EmptyBaggage;
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
