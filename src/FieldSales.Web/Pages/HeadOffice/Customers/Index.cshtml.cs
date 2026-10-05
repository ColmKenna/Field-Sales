using FieldSales.Directory.Contracts;
using FieldSales.Web.Directory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.HeadOffice.Customers;

public sealed class IndexModel(DirectoryApiClient directory) : PageModel
{
    public IReadOnlyList<CustomerSummary> Customers { get; private set; } = [];
    public async Task<IActionResult> OnGetAsync()
    {
        var result = await directory.CustomersAsync(HttpContext.RequestAborted);
        if (!result.Success) return StatusCode((int)result.Status);
        Customers = result.Value!;
        return Page();
    }
}
