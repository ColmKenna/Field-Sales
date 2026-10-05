using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FieldSales.Web.Security;

namespace FieldSales.Web.Pages;

[AllowAnonymous]
public class IndexModel : PageModel
{
    private readonly StaffAreaService _areas;

    public IndexModel(StaffAreaService areas) => _areas = areas;

    public bool SignInFailed { get; private set; }
    public bool SignedOut { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? error, bool signedOut = false, string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                StaffArea? requestedArea = StaffAreas.ForLocalUrl(returnUrl);
                if (requestedArea is null || User.IsInRole(requestedArea.Role)) return LocalRedirect(returnUrl!);
                return RedirectToPage("/AccessDenied");
            }

            StaffLanding landing = await _areas.ResolveLandingAsync(User, HttpContext.RequestAborted);
            if (landing.Status == StaffLandingStatus.Direct) return LocalRedirect(landing.Route!);
            if (landing.Status == StaffLandingStatus.Choose) return RedirectToPage("/Staff/Index");
            return RedirectToPage("/AccessDenied");
        }
        SignInFailed = error == "sign-in";
        SignedOut = signedOut;
        return Page();
    }
}
