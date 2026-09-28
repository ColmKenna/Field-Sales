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

            string? lastArea = await _areas.GetLastPermittedAreaAsync(User, HttpContext.RequestAborted);
            if (lastArea is not null) return LocalRedirect(lastArea);
            IReadOnlyList<StaffArea> permitted = StaffAreas.PermittedTo(User);
            if (permitted.Count == 1) return LocalRedirect(permitted[0].Route);
            if (permitted.Count > 1) return RedirectToPage("/Staff/Index");
            return RedirectToPage("/AccessDenied");
        }
        SignInFailed = error == "sign-in";
        SignedOut = signedOut;
        return Page();
    }
}
