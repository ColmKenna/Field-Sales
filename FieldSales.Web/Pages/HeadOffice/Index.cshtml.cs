using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using FieldSales.Web.Security;

namespace FieldSales.Web.Pages.HeadOffice;

public sealed class IndexModel(StaffAreaService areas) : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        await areas.RememberAreaAsync(User, StaffAreas.HeadOffice, HttpContext.RequestAborted);
        return Page();
    }
}
