using System.Net;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using FieldSales.Web.Coverage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.Coverage;

public sealed class GroupPermissionsModel(CoverageApiClient coverage, PermissionRemovalReviews reviews) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid? GroupId { get; set; }
    [BindProperty(SupportsGet = true)] public bool Remove { get; set; }
    [BindProperty] public List<CohortRowInput> Reps { get; set; } = [];
    [BindProperty] public string? Reason { get; set; }
    public IReadOnlyList<PermissionGroupChoice> Groups { get; private set; } = [];
    public GroupPermissionsPage? Permissions { get; private set; }
    public PermissionRemovalReview? RemovalReview { get; private set; }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ?? Page();

    public async Task<IActionResult> OnPostGrantAsync()
    {
        Remove = false;
        if (GroupId is null || GroupId == Guid.Empty) return BadRequest();
        if (!ModelState.IsValid) return await LoadAsync() ?? Page();
        var selections = Reps.Where(row => row.Selected)
            .Select(row => new CohortPermissionSelection(row.RepSubject, row.ExpectedLastChangeId)).ToArray();
        var result = await coverage.GrantCohortPermissionAsync(GroupId.Value, new(selections, Reason), HttpContext.RequestAborted);
        if (result.Success)
        {
            TempData["CohortPermissionNotice"] = $"Permission granted to {result.Value!.Changes.Count} reps. Changes take effect at each rep's next Sync.";
            return RedirectToPage(new { groupId = GroupId });
        }
        if (result.Status is not (HttpStatusCode.BadRequest or HttpStatusCode.Conflict)) return StatusCode((int)result.Status);
        ModelState.AddModelError(string.Empty, result.Error ?? "These permissions could not be saved.");
        // A retry must use the refreshed state, rather than carrying stale selected rows forward.
        Reps.Clear();
        return await LoadAsync() ?? Page();
    }

    public async Task<IActionResult> OnPostRemoveAsync()
    {
        Remove = true;
        if (GroupId is null || GroupId == Guid.Empty) return BadRequest();
        if (!ModelState.IsValid) return await LoadAsync() ?? Page();
        var selected = Reps.Where(row => row.Selected).ToArray();
        if (selected.Length == 0 || selected.Select(row => row.RepSubject).Distinct(StringComparer.Ordinal).Count() != selected.Length)
        {
            ModelState.AddModelError(string.Empty, selected.Length == 0
                ? "Select at least one rep to remove the permission from." : "Select each rep only once.");
            return await LoadAsync() ?? Page();
        }
        if (await LoadAsync() is { } failure) return failure;
        var reps = new List<PermissionRemovalRep>();
        foreach (var selection in selected)
        {
            var row = Permissions!.Reps.SingleOrDefault(row => row.Rep.Subject == selection.RepSubject);
            if (row is { Granted: true, CanRemove: false }) return StatusCode(StatusCodes.Status403Forbidden);
            if (row is null || !row.CanRemove || row.LastChange is null || row.LastChange.Id != selection.ExpectedLastChangeId)
            {
                ModelState.AddModelError(string.Empty, "These permissions changed. Review the selection again before saving.");
                return Page();
            }
            reps.Add(new(row.Rep.Subject, row.Rep.Name, row.LastChange.Id));
        }
        string? actor = User.GetStaffSubject();
        if (actor is null) return StatusCode(StatusCodes.Status403Forbidden);
        try
        {
            RemovalReview = reviews.Create(actor, Permissions!.Group.RestrictionGroupId, Permissions.Group.Name,
                Permissions.Group.Archived, reps, Reason, isCohort: true);
        }
        catch (ArgumentException exception) when (exception.ParamName == "reason")
        { ModelState.AddModelError(nameof(Reason), "Enter a reason of up to 1000 characters."); }
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmRemovalAsync(string? reviewProof, bool confirmed)
    {
        Remove = true;
        string? actor = User.GetStaffSubject();
        var state = actor is null ? null : reviews.Read(reviewProof, actor);
        if (!ModelState.IsValid || !confirmed || state is null || !state.IsCohort || state.GroupId != GroupId)
        {
            ModelState.AddModelError(string.Empty, "This removal review is invalid or expired. Review the selection again before confirming.");
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return await LoadAsync() ?? Page();
        }
        var result = await coverage.RemoveCohortPermissionAsync(state.GroupId,
            new(state.Reps.Select(rep => new CohortPermissionSelection(rep.Subject, rep.ExpectedLastChangeId)).ToArray(), state.Reason),
            HttpContext.RequestAborted);
        if (result.Success)
        {
            int count = result.Value!.Changes.Count;
            TempData["CohortPermissionNotice"] = $"Permission removed from {count} {(count == 1 ? "rep" : "reps")}. Changes take effect at each rep's next Sync.";
            return RedirectToPage(new { groupId = state.GroupId, remove = true });
        }
        if (result.Status is not (HttpStatusCode.BadRequest or HttpStatusCode.Conflict)) return StatusCode((int)result.Status);
        ModelState.AddModelError(string.Empty, result.Error ?? "These permissions could not be saved.");
        Reps.Clear();
        return await LoadAsync() ?? Page();
    }

    private async Task<IActionResult?> LoadAsync()
    {
        if ((!ModelState.IsValid && GroupId is null) || GroupId == Guid.Empty) return BadRequest();
        if (GroupId is null)
        {
            var groups = await coverage.PermissionGroupsAsync(HttpContext.RequestAborted, includeArchived: true);
            if (!groups.Success) return StatusCode((int)groups.Status);
            Groups = groups.Value!;
            return null;
        }
        var page = await coverage.GroupPermissionsAsync(GroupId.Value, HttpContext.RequestAborted);
        if (!page.Success) return StatusCode((int)page.Status);
        Permissions = page.Value!;
        if (Permissions.Group.Archived) Remove = true;
        return null;
    }

    public sealed class CohortRowInput
    {
        public string RepSubject { get; set; } = "";
        public bool Selected { get; set; }
        public Guid? ExpectedLastChangeId { get; set; }
    }
}
