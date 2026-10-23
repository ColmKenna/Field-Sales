using System.Net;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using FieldSales.Web.Coverage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.Coverage;

// M-14 per rep: every Restriction Group with Granted / Not granted and its last change. The API decides
// who may change; the page only offers the controls it reports.
public sealed class PermissionsModel(CoverageApiClient coverage, PermissionRemovalReviews reviews) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? RepSubject { get; set; }
    public IReadOnlyList<PermissionRepChoice> Reps { get; private set; } = [];
    public RepPermissionsPage? Permissions { get; private set; }
    public PermissionRemovalReview? RemovalReview { get; private set; }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ?? Page();

    public async Task<IActionResult> OnPostChangeAsync(Guid groupId, RestrictionPermissionChangeKind change, string? reason,
        Guid? expectedLastChangeId)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(RepSubject) || groupId == Guid.Empty || !Enum.IsDefined(change)) return BadRequest();
        if (change == RestrictionPermissionChangeKind.Granted)
            return await SaveAsync(groupId, new(change, reason, expectedLastChangeId));

        if (await LoadAsync() is { } failure) return failure;
        if (!Permissions!.CanChange) return StatusCode(StatusCodes.Status403Forbidden);
        var group = Permissions.Groups.SingleOrDefault(row => row.RestrictionGroupId == groupId);
        if (group is null) return NotFound();
        if (!group.CanRemove || group.LastChange is null || group.LastChange.Id != expectedLastChangeId)
        {
            ModelState.AddModelError(string.Empty, "This permission changed. Reload before saving.");
            return Page();
        }
        string? actor = User.GetStaffSubject();
        if (actor is null) return StatusCode(StatusCodes.Status403Forbidden);
        try
        {
            RemovalReview = reviews.Create(actor, group.RestrictionGroupId, group.Name, group.Archived,
                [new(Permissions.Rep.Subject, Permissions.Rep.Name, group.LastChange.Id)], reason);
        }
        catch (ArgumentException exception) when (exception.ParamName == nameof(reason))
        { ModelState.AddModelError(nameof(reason), "Enter a reason of up to 1000 characters."); }
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmRemovalAsync(string? reviewProof, bool confirmed)
    {
        string? actor = User.GetStaffSubject();
        var state = actor is null ? null : reviews.Read(reviewProof, actor);
        if (!ModelState.IsValid || !confirmed || state is null || state.IsCohort || state.Reps.Count != 1 || state.Reps[0].Subject != RepSubject)
        {
            ModelState.AddModelError(string.Empty, "This removal review is invalid or expired. Review the permission again before confirming.");
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return await LoadAsync() ?? Page();
        }
        return await SaveAsync(state.GroupId, new(RestrictionPermissionChangeKind.Removed, state.Reason, state.Reps[0].ExpectedLastChangeId));
    }

    private async Task<IActionResult> SaveAsync(Guid groupId, ChangeRestrictionPermissionRequest request)
    {
        var saved = await coverage.ChangePermissionAsync(RepSubject!, groupId, request,
            HttpContext.RequestAborted);
        if (saved.Success)
        {
            var entry = saved.Value!;
            string rep = entry.Rep.DisplayName;
            TempData["PermissionNotice"] = (request.Change == RestrictionPermissionChangeKind.Granted
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
