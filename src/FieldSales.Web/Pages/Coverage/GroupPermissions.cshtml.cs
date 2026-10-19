using System.Net;
using FieldSales.Directory.Contracts;
using FieldSales.Web.Coverage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.Coverage;

public sealed class GroupPermissionsModel(CoverageApiClient coverage) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid? GroupId { get; set; }
    [BindProperty] public List<CohortRowInput> Reps { get; set; } = [];
    [BindProperty] public string? Reason { get; set; }
    public IReadOnlyList<PermissionGroupChoice> Groups { get; private set; } = [];
    public GroupPermissionsPage? Permissions { get; private set; }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ?? Page();

    public async Task<IActionResult> OnPostGrantAsync()
    {
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

    private async Task<IActionResult?> LoadAsync()
    {
        if ((!ModelState.IsValid && GroupId is null) || GroupId == Guid.Empty) return BadRequest();
        if (GroupId is null)
        {
            var groups = await coverage.PermissionGroupsAsync(HttpContext.RequestAborted);
            if (!groups.Success) return StatusCode((int)groups.Status);
            Groups = groups.Value!;
            return null;
        }
        var page = await coverage.GroupPermissionsAsync(GroupId.Value, HttpContext.RequestAborted);
        if (!page.Success) return StatusCode((int)page.Status);
        Permissions = page.Value!;
        return null;
    }

    public sealed class CohortRowInput
    {
        public string RepSubject { get; set; } = "";
        public bool Selected { get; set; }
        public Guid? ExpectedLastChangeId { get; set; }
    }
}
