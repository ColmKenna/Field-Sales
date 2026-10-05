using System.Net;
using FieldSales.Directory.Contracts;
using FieldSales.Web.Coverage;
using FieldSales.StaffAccess;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.Coverage;

public sealed class LocationChangeModel(CoverageApiClient coverage, IDataProtectionProvider protection) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? EntryProof { get; set; }
    [BindProperty] public string? RepSubject { get; set; }
    [BindProperty] public string? Reason { get; set; }
    [BindProperty] public string? PreviewProof { get; set; }
    [BindProperty] public bool Confirmed { get; set; }
    public LocationCoverageActions Actions { get; private set; } = null!;
    public LocationChangeContext Entry { get; private set; } = null!;
    public IReadOnlyList<StaffChoice> Reps { get; private set; } = [];
    public AssignmentImpactDetails? Preview { get; private set; }
    public string? Notice { get; private set; }
    public string Heading => Entry.Intent == "Town" ? "Assign " + Actions.Location.TownName + " (Town)"
        : Entry.Intent == "Source" ? "Transfer " + Actions.Location.Owner!.SourceName + " (" + Entry.Source!.Level + ")"
        : (Actions.Location.Owner is null ? "Assign" : "Change") + " just this shop";

    public async Task<IActionResult> OnGetAsync(Guid id) => await LoadAsync(id) ?? Page();
    public async Task<IActionResult> OnPostPreviewAsync(Guid id)
    {
        if (await LoadAsync(id) is { } denied) return denied;
        if (!ValidRecipient()) return Page();
        if (Entry.IsTransfer)
            return RedirectToPage("./Transfer", new { repSubject = Entry.OwnerSubject, receivingRepSubject = RepSubject,
                pull = true, pullAssignmentId = Entry.SourceAssignmentId, reason = Reason, locationEntry = EntryProof });
        var result = await coverage.PreviewAddAsync(Command(), HttpContext.RequestAborted);
        if (result.Success) SetPreview(result.Value!);
        else if (result.Status is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            ModelState.AddModelError("", result.Error ?? "Coverage changed. Return to the Location and review it again.");
        else return StatusCode((int)result.Status);
        return Page();
    }
    public async Task<IActionResult> OnPostSaveAsync(Guid id)
    {
        if (await LoadAsync(id, true) is { } denied) return denied;
        if (Entry.IsTransfer) return BadRequest("Review this assignment through Transfer.");
        if (!ValidRecipient()) return Page();
        var result = await coverage.SaveAddAsync(Command(), HttpContext.RequestAborted);
        if (result.Success && result.Value?.Saved == true)
        {
            TempData["AssignmentNotice"] = "Assignment change saved.";
            return RedirectToPage("./Location", new { id });
        }
        if (result.Value?.Preview is { } fresh) { SetPreview(fresh); Notice = "Nothing was saved. " + result.Error; }
        else if (result.Status is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            ModelState.AddModelError("", result.Error ?? "Nothing was saved. Review this change again.");
        else return StatusCode((int)result.Status);
        return Page();
    }
    private AddTerritoryAssignmentRequest Command() => new(RepSubject, Entry.Target, Reason, PreviewProof, Confirmed);
    private void SetPreview(AssignmentImpactDetails preview)
    {
        Preview = preview; PreviewProof = preview.Proof; Confirmed = false;
        ModelState.Remove(nameof(PreviewProof)); ModelState.Remove(nameof(Confirmed));
    }
    private bool ValidRecipient()
    {
        if (!Reps.Any(rep => rep.Subject == RepSubject)) ModelState.AddModelError(nameof(RepSubject), "Choose an eligible rep whose assignments you can manage.");
        return ModelState.IsValid;
    }
    private async Task<IActionResult?> LoadAsync(Guid id, bool saving = false)
    {
        var entry = LocationChangeContext.Read(protection, User, EntryProof);
        if (entry is null || entry.LocationId != id) return BadRequest("Return to the Location and choose a coverage action.");
        var result = await coverage.LocationActionsAsync(id, HttpContext.RequestAborted);
        if (!result.Success) return StatusCode((int)result.Status);
        Actions = result.Value!; Entry = entry;
        if (!entry.Matches(Actions, saving)) return StatusCode(409, "Coverage changed. Return to the Location and review it again.");
        if (entry.IsTransfer)
        {
            var review = await coverage.TransferReviewAsync(entry.OwnerSubject!, HttpContext.RequestAborted);
            if (!review.Success) return StatusCode((int)review.Status);
            if (!review.Value!.Assignments.Any(row => row.Selection.AssignmentId == entry.SourceAssignmentId && row.Selection.Target == entry.Target))
                return StatusCode(409, "The source assignment changed. Return to the Location.");
            Reps = review.Value.ReceivingReps;
        }
        else
        {
            var options = await coverage.OptionsAsync(null, HttpContext.RequestAborted);
            if (!options.Success) return StatusCode((int)options.Status);
            if (!options.Value!.Targets.Any(row => row.Target == entry.Target)) return StatusCode(409, "This assignment target is no longer available.");
            Reps = options.Value.Reps.Where(rep => rep.Subject != Actions.Location.Owner?.Rep.Subject).ToArray();
            // The stored assignment's RepSubject references RepReportingLines.
            // General Add previews can include an unconfigured HO recipient;
            // this focused chooser offers only changes the existing schema can save.
            if (User.IsInRole(BusinessRoles.HeadOfficeUser))
            {
                var reporting = await coverage.ReportingLinesAsync(HttpContext.RequestAborted);
                if (!reporting.Success) return StatusCode((int)reporting.Status);
                var configured = reporting.Value!.Lines.Select(row => row.Line.RepSubject).ToHashSet(StringComparer.Ordinal);
                Reps = Reps.Where(rep => configured.Contains(rep.Subject)).ToArray();
            }
        }
        return null;
    }
}
