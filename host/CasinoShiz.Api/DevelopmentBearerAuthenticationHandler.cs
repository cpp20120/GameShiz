using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CasinoShiz.Api;

internal sealed class DevelopmentBearerAuthenticationHandler(
    IOptionsMonitor<DevelopmentAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<DevelopmentAuthenticationOptions>(options, logger, encoder)
{
    public const string SchemeName = "RestDevelopment";
    private const string LoadTestUserIdHeader = "X-Load-Test-User-Id";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.FirstOrDefault();
        const string prefix = "Bearer ";
        if (authorization is null || !authorization.StartsWith(prefix, StringComparison.Ordinal))
            return Task.FromResult(AuthenticateResult.NoResult());

        var suppliedToken = authorization[prefix.Length..];
        var configuredToken = Options.Token;
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedToken);
        var configuredBytes = Encoding.UTF8.GetBytes(configuredToken);
        var isValid = configuredBytes.Length > 0
            && CryptographicOperations.FixedTimeEquals(suppliedBytes, configuredBytes);

        if (!isValid)
            return Task.FromResult(AuthenticateResult.NoResult());

        var userId = Options.UserId;
        var displayName = Options.DisplayName;
        if (Options.AllowLoadTestIdentityOverride
            && Request.Headers.TryGetValue(LoadTestUserIdHeader, out var requestedUserId))
        {
            if (!long.TryParse(requestedUserId, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedUserId)
                || parsedUserId <= 0)
            {
                return Task.FromResult(AuthenticateResult.Fail(
                    $"{LoadTestUserIdHeader} must be a positive 64-bit integer."));
            }

            userId = parsedUserId.ToString(CultureInfo.InvariantCulture);
            displayName = $"REST load-test user {userId}";
        }

        var claims = new[]
        {
            new Claim("sub", userId),
            new Claim("name", displayName),
            new Claim("tenant_id", Options.TenantId),
            new Claim("scope_id", Options.ScopeId),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
