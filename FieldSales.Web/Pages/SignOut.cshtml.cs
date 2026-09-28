using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages;

public sealed class SignOutModel : PageModel
{
    public IActionResult OnPost() => SignOut(
        new AuthenticationProperties { RedirectUri = "/?signedOut=true" },
        CookieAuthenticationDefaults.AuthenticationScheme,
        OpenIdConnectDefaults.AuthenticationScheme);
}
