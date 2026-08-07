using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
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

        var claims = new[]
        {
            new Claim("sub", Options.UserId),
            new Claim("name", Options.DisplayName),
            new Claim("tenant_id", Options.TenantId),
            new Claim("scope_id", Options.ScopeId),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
