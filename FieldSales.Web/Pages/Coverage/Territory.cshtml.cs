using FieldSales.Directory.Contracts;
using FieldSales.Web.Coverage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.Coverage;

public sealed class TerritoryModel(CoverageApiClient coverage) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? RepSubject { get; set; }
    [BindProperty(SupportsGet = true)] public string? Filter { get; set; }
    public IReadOnlyList<StaffChoice> Reps { get; private set; } = [];
    public RepTerritoryPage? Territory { get; private set; }
    public Dictionary<Guid, string[]> MatchingCounties { get; private set; } = [];
    public IReadOnlyList<RepTerritoryAssignment> VisibleAssignments { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(RepSubject))
        {
            var options = await coverage.OptionsAsync(null, HttpContext.RequestAborted);
            if (!options.Success) return StatusCode((int)options.Status);
            Reps = options.Value!.Reps; return Page();
        }
        var result = await coverage.TerritoryAsync(RepSubject, HttpContext.RequestAborted);
        if (!result.Success) return StatusCode((int)result.Status);
        Territory = result.Value!;
        string search = Filter?.Trim() ?? "";
        bool Matches(string text) => text.Contains(search, StringComparison.OrdinalIgnoreCase);
        if (search.Length > 0 && Territory.Assignments.Any(row => row.Assignment.Assignment.Target.Level == TerritoryLevel.Region))
        {
            var transfer = await coverage.TransferReviewAsync(RepSubject, HttpContext.RequestAborted);
            if (!transfer.Success) return StatusCode((int)transfer.Status);
            MatchingCounties = transfer.Value!.Assignments.Where(row => row.Selection.Target.Level == TerritoryLevel.Region)
                .ToDictionary(row => row.Selection.AssignmentId, row => row.Children.Where(child => Matches(child.Name)).Select(child => child.Name).ToArray());
        }
        VisibleAssignments = Territory.Assignments.Select(row => Matches(row.Assignment.Name) || Matches(row.Context)
                ? row : row with { Towns = row.Towns.Where(town => Matches(town.Name)).ToArray() })
            .Where(row => Matches(row.Assignment.Name) || Matches(row.Context) || row.Towns.Count > 0
                || MatchingCounties.TryGetValue(row.Assignment.Assignment.Id, out var matches) && matches.Length > 0).ToArray();
        return Page();
    }
}
