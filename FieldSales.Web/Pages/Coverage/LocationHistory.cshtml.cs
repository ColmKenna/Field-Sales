using FieldSales.Directory.Contracts;
using FieldSales.Web.Coverage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.Coverage;

public sealed class LocationHistoryModel(CoverageApiClient coverage) : PageModel
{
    public LocationCoverageHistoryPage History { get; private set; } = null!;
    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var result = await coverage.LocationHistoryAsync(id, HttpContext.RequestAborted);
        if (!result.Success) return StatusCode((int)result.Status);
        History = result.Value!;
        return Page();
    }
}
