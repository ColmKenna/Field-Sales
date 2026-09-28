using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages;

[AllowAnonymous]
public class IndexModel : PageModel
{
    public bool SignInFailed { get; private set; }
    public bool SignedOut { get; private set; }

    public IActionResult OnGet(string? error, bool signedOut = false)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToPage("/Staff/Index");
        SignInFailed = error == "sign-in";
        SignedOut = signedOut;
        return Page();
    }
}
