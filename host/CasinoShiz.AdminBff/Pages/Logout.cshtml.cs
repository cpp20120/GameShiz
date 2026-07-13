using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace CasinoShiz.AdminBff.Pages;
public sealed class LogoutModel : PageModel
{
    public IActionResult OnGet(){ HttpContext.Session.SignOut(); return RedirectToPage("/Login"); }
}
