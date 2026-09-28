using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages;

[AllowAnonymous]
public sealed class SignInModel : PageModel
{
    public IActionResult OnGet() => BeginSignIn();

    public IActionResult OnPost() => BeginSignIn();

    private IActionResult BeginSignIn()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToPage("/Staff/Index");
        return Challenge(new AuthenticationProperties { RedirectUri = "/Staff" },
            OpenIdConnectDefaults.AuthenticationScheme);
    }
}
