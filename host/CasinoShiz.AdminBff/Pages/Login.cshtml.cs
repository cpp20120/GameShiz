using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.AdminBff.Pages;

public sealed class LoginModel(IConfiguration configuration) : PageModel
{
    [BindProperty] public string Token { get; set; } = "";
    public string? Error { get; private set; }
    public IActionResult OnGet() => HttpContext.Session.IsAuthenticated() ? RedirectToPage("/Index") : Page();
    public IActionResult OnPost()
    {
        var superAdminToken = configuration["Admin:SuperAdminToken"];
        if (string.IsNullOrWhiteSpace(superAdminToken))
            superAdminToken = configuration["Admin:WebToken"];
        var readOnlyToken = configuration["Admin:ReadOnlyToken"];
        string? role = null;
        if (Matches(superAdminToken, Token))
            role = "SuperAdmin";
        else if (Matches(readOnlyToken, Token))
            role = "Admin";
        if (role is null)
        {
            Error = "Invalid token or Admin:WebToken is not configured.";
            return Page();
        }
        var actorId = configuration.GetValue<long?>("Admin:ActorId") ?? 1;
        var actorName = configuration["Admin:DisplayName"] ?? role;
        HttpContext.Session.SignIn(role, actorId, actorName);
        return RedirectToPage("/Index");
    }
    private static bool FixedEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static bool Matches(string? expected, string supplied) =>
        !string.IsNullOrWhiteSpace(expected) && FixedEquals(expected, supplied);
}
