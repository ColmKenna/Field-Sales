using FieldSales.Directory.Contracts;
using FieldSales.Web.Coverage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.Coverage;

public sealed class UnassignedModel(CoverageApiClient coverage, IDataProtectionProvider protection) : PageModel
{
    public UnassignedCoveragePage Coverage { get; private set; } = null!;
    public string Entry(UnassignedLocation row, string intent) =>
        LocationChangeContext.Issue(protection, User, row.Actions, intent, fromUnassigned: true);
    public async Task<IActionResult> OnGetAsync()
    {
        var result = await coverage.UnassignedAsync(HttpContext.RequestAborted);
        if (!result.Success) return StatusCode((int)result.Status);
        Coverage = result.Value!;
        return Page();
    }
}
