using System.Net;
using FieldSales.Directory.Contracts;
using FieldSales.Web.Coverage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.Coverage;

public sealed class TransferModel(CoverageApiClient coverage, IDataProtectionProvider protection) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? RepSubject { get; set; }
    [BindProperty(SupportsGet = true)] public string? Filter { get; set; }
    [BindProperty] public List<string> Selected { get; set; } = [];
    [BindProperty] public List<string> ReviewedSelections { get; set; } = [];
    [BindProperty(SupportsGet = true)] public string? ReceivingRepSubject { get; set; }
    [BindProperty(SupportsGet = true)] public bool Pull { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? PullAssignmentId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Reason { get; set; }
    [BindProperty(SupportsGet = true)] public string? LocationEntry { get; set; }
    public Guid? ReturnLocationId { get; private set; }
    public string? ReturnLocationName { get; private set; }
    public string ReturnUrl => ReturnLocationId.HasValue ? Url.Page("./Location", new { id = ReturnLocationId })!
        : Url.Page("./Territory", new { repSubject = ReturnRepSubject })!;
    [BindProperty] public string? PreviewProof { get; set; }
    [BindProperty] public bool Confirmed { get; set; }
    public TransferReview Review { get; private set; } = new(new("", ""), [], []);
    public IReadOnlyList<TransferScope> Visible { get; private set; } = [];
    public AssignmentImpactDetails? Preview { get; private set; }
    public string? Notice { get; private set; }
    public bool ChoosingRecipient { get; private set; }
    public StaffChoice? Receiver => Pull ? Review.ReceivingReps.SingleOrDefault(row => row.Subject == ReceivingRepSubject) : null;
    public string? ReturnRepSubject => Pull ? ReceivingRepSubject : RepSubject;
    public string ReturnRepName => Receiver?.Name ?? Review.SourceRep.Name;
    public static string Key(TransferScope row) => $"{row.Selection.AssignmentId:D}:{row.Selection.Target.Level}:{row.Selection.Target.UnitId:D}";
    public bool Matches(string value) => value.Contains(Filter?.Trim() ?? "", StringComparison.OrdinalIgnoreCase);

    public async Task<IActionResult> OnGetAsync()
    {
        if (await LoadAsync() is { } denied) return denied;
        foreach (var row in Visible)
        {
            if (PullAssignmentId.HasValue || Matches(row.Name) || Matches(row.Context)) Selected.Add(Key(row));
            Selected.AddRange(row.Children.Where(child => child.AssignedTo is null && (PullAssignmentId.HasValue || Matches(row.Name) || Matches(row.Context) || Matches(child.Name))).Select(Key));
        }
        return Page();
    }
    public async Task<IActionResult> OnPostChooseAsync()
    {
        if (await LoadAsync() is { } denied) return denied;
        if (Selections() is not null) ChoosingRecipient = !Pull;
        return Page();
    }
    public async Task<IActionResult> OnPostPreviewAsync()
    {
        if (await LoadAsync() is { } denied) return denied;
        ChoosingRecipient = !Pull;
        var selected = Selections(); if (selected is null) return Page();
        var result = await coverage.PreviewTransferAsync(Command(selected), HttpContext.RequestAborted);
        if (result.Success) { ReviewedSelections = selected.Select(row => $"{row.AssignmentId:D}:{row.Target.Level}:{row.Target.UnitId:D}").ToList(); SetPreview(result.Value!); }
        else if (result.Status is HttpStatusCode.BadRequest or HttpStatusCode.Conflict) ModelState.AddModelError("", result.Error ?? "Review the selection again.");
        else return StatusCode((int)result.Status);
        return Page();
    }
    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (await LoadAsync() is { } denied) return denied;
        ChoosingRecipient = !Pull;
        var selected = Reviewed(); if (selected is null) return Page();
        var result = await coverage.SaveTransferAsync(Command(selected), HttpContext.RequestAborted);
        if (result.Success && result.Value?.Saved == true)
        {
            TempData["AssignmentNotice"] = "Assignments transferred.";
            return ReturnLocationId.HasValue ? RedirectToPage("./Location", new { id = ReturnLocationId })
                : RedirectToPage("./Territory", new { repSubject = ReturnRepSubject });
        }
        if (result.Value?.Preview is { } refreshed) { SetPreview(refreshed); Notice = "Nothing was saved. " + result.Error; }
        else if (result.Status is HttpStatusCode.BadRequest or HttpStatusCode.Conflict) ModelState.AddModelError("", result.Error ?? "Nothing was saved. Review the selection again.");
        else return StatusCode((int)result.Status);
        return Page();
    }
    private TransferAssignmentsRequest Command(IReadOnlyList<TransferSelection> selections) => new(RepSubject, ReceivingRepSubject, selections, Reason, PreviewProof, Confirmed);
    private IReadOnlyList<TransferSelection>? Selections()
    {
        var rows = PullAssignmentId.HasValue ? Visible : Review.Assignments;
        var allowed = rows.SelectMany(row => row.Children.Where(child => child.AssignedTo is null).Prepend(row)).ToDictionary(Key);
        if (Selected.Count == 0 || Selected.Any(key => !allowed.ContainsKey(key)))
        { ModelState.AddModelError("", "Select at least one of this rep's assignments or areas. Reload if the selection changed."); return null; }
        List<TransferSelection> result = [];
        foreach (var row in rows)
        {
            var children = row.Children.Where(child => child.AssignedTo is null).ToArray();
            // Children are authoritative for exclusions, even if a parent checkbox was submitted.
            // JavaScript only synchronizes the controls; the server decides whether the parent moves.
            if (Selected.Contains(Key(row)) && children.All(child => Selected.Contains(Key(child)))) result.Add(row.Selection);
            else result.AddRange(children.Where(child => Selected.Contains(Key(child))).Select(child => child.Selection));
        }
        if (result.Count == 0) { ModelState.AddModelError("", "Select at least one assignment or area."); return null; }
        return result;
    }
    private IReadOnlyList<TransferSelection>? Reviewed()
    {
        List<TransferSelection> result = [];
        foreach (string key in ReviewedSelections)
        {
            var parts = key.Split(':');
            if (parts.Length != 3 || !Guid.TryParse(parts[0], out var id) || !Enum.TryParse<TerritoryLevel>(parts[1], out var level)
                || !Enum.IsDefined(level) || !Guid.TryParse(parts[2], out var unit))
            { ModelState.AddModelError("", "Review the selection again."); return null; }
            result.Add(new(id, new(level, unit)));
        }
        if (LocationEntry is not null)
        {
            var allowed = Visible.SelectMany(row => row.Children.Where(child => child.AssignedTo is null).Prepend(row)).Select(row => row.Selection).ToHashSet();
            if (result.Any(row => !allowed.Contains(row)))
            { ModelState.AddModelError("", "Return to the Location and review this assignment again."); return null; }
        }
        if (result.Count == 0) { ModelState.AddModelError("", "Review the selection again."); return null; }
        return result;
    }
    private void SetPreview(AssignmentImpactDetails value)
    {
        Preview = value; PreviewProof = value.Proof; Confirmed = false;
        ModelState.Remove(nameof(PreviewProof)); ModelState.Remove(nameof(Confirmed));
    }
    private async Task<IActionResult?> LoadAsync()
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(RepSubject)) return BadRequest("Review the transfer context again.");
        if (LocationEntry is not null)
        {
            var entry = LocationChangeContext.Read(protection, User, LocationEntry);
            if (entry is null || !entry.IsTransfer || !Pull || PullAssignmentId != entry.SourceAssignmentId || RepSubject != entry.OwnerSubject)
                return BadRequest("Return to the Location and review this transfer again.");
            var context = await coverage.LocationActionsAsync(entry.LocationId, HttpContext.RequestAborted);
            if (!context.Success) return StatusCode((int)context.Status);
            if (!entry.Matches(context.Value!)) return StatusCode(409, "Coverage changed. Return to the Location and review it again.");
            ReturnLocationId = entry.LocationId; ReturnLocationName = context.Value!.Location.Name;
        }
        var result = await coverage.TransferReviewAsync(RepSubject, HttpContext.RequestAborted);
        if (!result.Success) return StatusCode((int)result.Status);
        Review = result.Value!;
        if (Pull && Receiver is null) return BadRequest("Choose an active receiving rep whose assignments you can manage.");
        if (PullAssignmentId.HasValue && (!Pull || !Review.Assignments.Any(row => row.Selection.AssignmentId == PullAssignmentId)))
            return BadRequest("This assignment changed. Return to the receiving rep and review the choices again.");
        Visible = Review.Assignments.Where(row => (Matches(row.Name) || Matches(row.Context)) || row.Children.Any(child => Matches(child.Name))).ToArray();
        if (PullAssignmentId.HasValue) Visible = Review.Assignments.Where(row => row.Selection.AssignmentId == PullAssignmentId).ToArray();
        return null;
    }
}
