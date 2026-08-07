using Microsoft.AspNetCore.Authentication;

namespace CasinoShiz.Api;

internal sealed class DevelopmentAuthenticationOptions : AuthenticationSchemeOptions
{
    public string Token { get; set; } = string.Empty;

    public string TenantId { get; set; } = "e2e";

    public string ScopeId { get; set; } = "42";

    public string UserId { get; set; } = "42";

    public string DisplayName { get; set; } = "REST development user";
}
