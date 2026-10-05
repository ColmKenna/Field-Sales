using FieldSales.Directory.Contracts;
using FieldSales.Web.Coverage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.Coverage;

public sealed class LocationModel(CoverageApiClient coverage, IDataProtectionProvider protection) : PageModel
{
    public LocationCoverageActions Actions { get; private set; } = null!;
    public LocationCoveragePage Location => Actions.Location;
    public string Entry(string intent) => LocationChangeContext.Issue(protection, User, Actions, intent);
    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var result = await coverage.LocationActionsAsync(id, HttpContext.RequestAborted);
        if (!result.Success) return StatusCode((int)result.Status);
        Actions = result.Value!;
        return Page();
    }
}
