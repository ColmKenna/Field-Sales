using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using FieldSales.Web.Security;
using FieldSales.Web.Catalogue;

namespace FieldSales.Web.Pages.Staff;

public sealed class IndexModel(CatalogueApiClient catalogue, StaffAreaService areaService) : PageModel
{
    public bool ApiAvailable { get; private set; }
    public IReadOnlyList<StaffArea> PermittedAreas { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        StaffLanding landing = await areaService.ResolveLandingAsync(User, HttpContext.RequestAborted);
        PermittedAreas = landing.Areas;
        if (landing.Status == StaffLandingStatus.Direct) return LocalRedirect(landing.Route!);
        if (landing.Status == StaffLandingStatus.Denied) return RedirectToPage("/AccessDenied");

        CatalogueReadResult<FieldSales.StaffAccess.StaffSessionResponse> session =
            await catalogue.StaffSessionAsync(HttpContext.RequestAborted);
        ApiAvailable = session.Found;
        return Page();
    }
}
