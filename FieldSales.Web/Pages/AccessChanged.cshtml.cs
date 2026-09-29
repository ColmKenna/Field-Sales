using FieldSales.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages;

public sealed class AccessChangedModel(AccessChangedTokenService tokens) : PageModel
{
    public StaffArea Area { get; private set; } = null!;
    public bool WasWrite { get; private set; }
    public IReadOnlyList<StaffArea> RemainingAreas { get; private set; } = [];
    public StaffArea? PrimaryArea => RemainingAreas.FirstOrDefault();

    public IActionResult OnGet(string? state)
    {
        AccessChangedState? change = tokens.Read(state);
        StaffArea? area = StaffAreas.Find(change?.AreaKey);
        if (change is null || area is null
            || change.Subject != User.FindFirst("sub")?.Value
            || User.IsInRole(area.Role)
            || !User.HasClaim(StaffCookieEvents.RemovedRoleClaimType, area.Role))
            return RedirectToPage("/AccessDenied");

        Area = area;
        WasWrite = change.WasWrite;
        RemainingAreas = StaffAreas.PermittedTo(User);
        return Page();
    }
}
