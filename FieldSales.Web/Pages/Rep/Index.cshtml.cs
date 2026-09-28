using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using FieldSales.Web.Security;

namespace FieldSales.Web.Pages.Rep;

public sealed class IndexModel(StaffAreaService areas) : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        await areas.RememberAreaAsync(User, StaffAreas.Rep, HttpContext.RequestAborted);
        return Page();
    }
}
