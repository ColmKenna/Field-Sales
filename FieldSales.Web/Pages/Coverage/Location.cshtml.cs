using FieldSales.Directory.Contracts;
using FieldSales.Web.Coverage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.Coverage;

public sealed class LocationModel(CoverageApiClient coverage) : PageModel
{
    public LocationCoveragePage Location { get; private set; } = null!;
    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var result = await coverage.LocationAsync(id, HttpContext.RequestAborted);
        if (!result.Success) return StatusCode((int)result.Status);
        Location = result.Value!;
        return Page();
    }
}
