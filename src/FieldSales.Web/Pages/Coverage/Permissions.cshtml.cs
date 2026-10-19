using System.Net;
using FieldSales.Directory.Contracts;
using FieldSales.Web.Coverage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.Coverage;

// M-14 per rep: every Restriction Group with Granted / Not granted and its last change. The API decides
// who may change; the page only offers the controls it reports.
public sealed class PermissionsModel(CoverageApiClient coverage) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? RepSubject { get; set; }
    public IReadOnlyList<PermissionRepChoice> Reps { get; private set; } = [];
    public RepPermissionsPage? Permissions { get; private set; }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ?? Page();

    public async Task<IActionResult> OnPostChangeAsync(Guid groupId, RestrictionPermissionChangeKind change, string? reason,
        Guid? expectedLastChangeId)
    {
        if (string.IsNullOrWhiteSpace(RepSubject) || groupId == Guid.Empty || !Enum.IsDefined(change)) return BadRequest();
        var saved = await coverage.ChangePermissionAsync(RepSubject, groupId, new(change, reason, expectedLastChangeId),
            HttpContext.RequestAborted);
        if (saved.Success)
        {
            var entry = saved.Value!;
            string rep = entry.Rep.DisplayName;
            TempData["PermissionNotice"] = (change == RestrictionPermissionChangeKind.Granted
                ? $"{entry.RestrictionGroupName} granted to {rep}."
                : $"{entry.RestrictionGroupName} removed from {rep}.") + $" This takes effect at {rep}'s next Sync.";
            return RedirectToPage(new { repSubject = RepSubject });
        }
        if (saved.Status is not (HttpStatusCode.BadRequest or HttpStatusCode.Conflict)) return StatusCode((int)saved.Status);
        ModelState.AddModelError(string.Empty, saved.Error ?? "This change could not be saved.");
        return await LoadAsync() ?? Page();
    }

    private async Task<IActionResult?> LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(RepSubject))
        {
            var reps = await coverage.PermissionRepsAsync(HttpContext.RequestAborted);
            if (!reps.Success) return StatusCode((int)reps.Status);
            Reps = reps.Value!;
            return null;
        }
        var page = await coverage.RepPermissionsAsync(RepSubject, HttpContext.RequestAborted);
        if (!page.Success) return StatusCode((int)page.Status);
        Permissions = page.Value!;
        return null;
    }
}
