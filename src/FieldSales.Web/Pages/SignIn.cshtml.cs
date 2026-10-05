using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages;

[AllowAnonymous]
public sealed class SignInModel : PageModel
{
    public IActionResult OnGet(string? returnUrl = null) => BeginSignIn(returnUrl);

    public IActionResult OnPost(string? returnUrl = null) => BeginSignIn(returnUrl);

    private IActionResult BeginSignIn(string? returnUrl)
    {
        string destination = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(destination);
        return Challenge(new AuthenticationProperties { RedirectUri = destination },
            OpenIdConnectDefaults.AuthenticationScheme);
    }
}
