using FieldSales.Directory.Contracts;
using FieldSales.Web.Directory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.HeadOffice.Customers;

public sealed class DetailModel(DirectoryApiClient directory) : PageModel
{
    public CustomerDetails Customer { get; private set; } = null!;
    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var result = await directory.CustomerAsync(id, HttpContext.RequestAborted);
        if (!result.Success) return StatusCode((int)result.Status);
        Customer = result.Value!;
        return Page();
    }
}
