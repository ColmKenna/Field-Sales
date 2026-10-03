using System.Net;
using FieldSales.Directory.Contracts;
using FieldSales.Web.Coverage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.Coverage;

public sealed class AssignmentsModel(CoverageApiClient coverage) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? RepSubject { get; set; }
    [BindProperty] public string? TargetKey { get; set; }
    [BindProperty] public Guid? AssignmentId { get; set; }
    [BindProperty] public string? Version { get; set; }
    [BindProperty] public string? Reason { get; set; }
    [BindProperty] public string? Action { get; set; }
    [BindProperty] public string? PreviewProof { get; set; }
    [BindProperty] public bool Confirmed { get; set; }
    public AssignmentReviewOptions Options { get; private set; } = new([], [], []);
    public AssignmentImpactDetails? Preview { get; private set; }
    public string? Notice { get; private set; }
    public string? RepName => Options.Reps.SingleOrDefault(rep => rep.Subject == RepSubject)?.Name;

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ?? Page();

    public async Task<IActionResult> OnPostPreviewAsync()
    {
        if (!ValidateCommand()) return await LoadAsync() ?? Page();
        if (Action == "Add")
        {
            var result = await coverage.PreviewAddAsync(AddRequest(), HttpContext.RequestAborted);
            if (result.Success) SetPreview(result.Value!);
            else if (result.Status is HttpStatusCode.BadRequest or HttpStatusCode.Conflict) ModelState.AddModelError("", result.Error ?? "This change cannot be previewed.");
            else return StatusCode((int)result.Status);
        }
        else
        {
            var result = await coverage.PreviewRemoveAsync(AssignmentId!.Value, RemoveRequest(), HttpContext.RequestAborted);
            if (result.Success) SetPreview(result.Value!);
            else if (result.Status is HttpStatusCode.BadRequest or HttpStatusCode.Conflict) ModelState.AddModelError("", result.Error ?? "Reload the assignment before reviewing it.");
            else return StatusCode((int)result.Status);
        }
        return await LoadAsync() ?? Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!ValidateCommand()) return await LoadAsync() ?? Page();
        var result = Action == "Add" ? await coverage.SaveAddAsync(AddRequest(), HttpContext.RequestAborted)
            : await coverage.SaveRemoveAsync(AssignmentId!.Value, RemoveRequest(), HttpContext.RequestAborted);
        if (result.Success && result.Value?.Saved == true)
        {
            TempData["AssignmentNotice"] = "Assignment change saved.";
            return RedirectToPage(new { repSubject = RepSubject });
        }
        if (result.Value?.Preview is { } refreshed)
        {
            SetPreview(refreshed); Notice = result.Error;
        }
        else if (result.Status is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            ModelState.AddModelError("", result.Error ?? "This change was not saved. Review it again.");
        else return StatusCode((int)result.Status);
        return await LoadAsync() ?? Page();
    }

    private void SetPreview(AssignmentImpactDetails value)
    {
        Preview = value; PreviewProof = value.Proof; Version = value.AssignmentVersion;
        // Tag helpers must render the fresh server proof/version rather than
        // ModelState's submitted stale values. A new action click confirms it.
        ModelState.Remove(nameof(PreviewProof)); ModelState.Remove(nameof(Version)); ModelState.Remove(nameof(Confirmed));
        Confirmed = false;
    }
    private bool ValidateCommand()
    {
        if (Action is not ("Add" or "Remove")) ModelState.AddModelError("", "Choose an assignment change.");
        if (string.IsNullOrWhiteSpace(RepSubject)) ModelState.AddModelError(nameof(RepSubject), "Choose a field salesperson.");
        if (Action == "Add" && Target() is null) ModelState.AddModelError(nameof(TargetKey), "Choose a territory or location.");
        if (Action == "Remove" && (AssignmentId is null || AssignmentId == Guid.Empty)) ModelState.AddModelError(nameof(AssignmentId), "Choose an assignment.");
        return ModelState.IsValid;
    }
    private TerritoryTarget? Target()
    {
        var parts = TargetKey?.Split(':');
        return parts is { Length: 2 } && Enum.TryParse<TerritoryLevel>(parts[0], out var level) && Enum.IsDefined(level)
            && Guid.TryParse(parts[1], out var id) && id != Guid.Empty ? new(level, id) : null;
    }
    private AddTerritoryAssignmentRequest AddRequest() => new(RepSubject, Target(), Reason, PreviewProof, Confirmed);
    private RemoveTerritoryAssignmentRequest RemoveRequest() => new(Version, Reason, PreviewProof, Confirmed);
    private async Task<IActionResult?> LoadAsync()
    {
        var result = await coverage.OptionsAsync(RepSubject, HttpContext.RequestAborted);
        if (!result.Success) return StatusCode((int)result.Status);
        Options = result.Value!; return null;
    }
}
