using System.Net;
using FieldSales.Directory.Contracts;
using FieldSales.ReferenceData;
using FieldSales.Web.Directory;
using FieldSales.Web.ReferenceData;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.HeadOffice.Geography;

[RequestSizeLimit(GeographyImportLimits.MaximumBytes + 64 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = GeographyImportLimits.MaximumBytes)]
public sealed class IndexModel(DirectoryApiClient directory) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid? RegionId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? CountyId { get; set; }
    [BindProperty(SupportsGet = true)] public bool ShowArchived { get; set; }
    [BindProperty] public Guid? Id { get; set; }
    [BindProperty] public ReferenceAction Action { get; set; }
    [BindProperty] public string? Name { get; set; }
    [BindProperty] public Guid? RenameId { get; set; }
    [BindProperty] public string? RenameName { get; set; }
    [BindProperty] public string? Version { get; set; }
    [BindProperty] public IFormFile? Upload { get; set; }
    [TempData] public string? Notice { get; set; }
    public GeographyPage Geography { get; private set; } = null!;
    public string Singular => Geography.Level switch { "regions" => "Region", "counties" => "County", _ => "Town" };
    public string Plural => Geography.Level switch { "regions" => "Regions", "counties" => "Counties", _ => "Towns" };
    public GeographyItem? Confirmation { get; private set; }
    public bool CanAdd => Geography.Path.All(part => !part.IsArchived);
    public ReferenceConfirmationModel? ConfirmationPanel => Confirmation is { Usage: { } usage } item
        ? new(new(item.Id, item.Name, item.IsArchived, usage), new(Geography.Level, Singular, Plural, []),
            Url.Page("Index", "Retire", new { RegionId, CountyId, ShowArchived })!,
            Url.Page("Index", new { RegionId, CountyId, ShowArchived })!,
            new Dictionary<string, string> { ["Id"] = item.Id.ToString(), ["Version"] = item.Version },
            "It will be offered again for new selections when its parent hierarchy is active.") : null;

    public Task<IActionResult> OnGetAsync() => ShowAsync();
    public async Task<IActionResult> OnGetConfirmAsync(Guid id)
    {
        IActionResult page = await ShowAsync();
        if (page is not PageResult) return page;
        if (!Geography.Items.Any(item => item.Id == id)) return NotFound();
        var result = await directory.FindAsync(Geography.Level, id, HttpContext.RequestAborted);
        if (!result.Success) return StatusCode((int)result.Status);
        if (result.Value!.Usage is null) return StatusCode(503);
        Confirmation = result.Value;
        return Page();
    }
    public async Task<IActionResult> OnPostRetireAsync()
    {
        IActionResult page = await ShowAsync();
        if (page is not PageResult) return page;
        if (!ModelState.IsValid || !Request.Form.ContainsKey(nameof(Action)) || Id is not Guid id || !Enum.IsDefined(Action)
            || !Geography.Items.Any(item => item.Id == id)) return BadRequest();
        return await SavedAsync(await directory.RetireAsync(Geography.Level, id, Action, Version, HttpContext.RequestAborted), string.Empty);
    }
    public async Task<IActionResult> OnPostCreateAsync()
    {
        IActionResult page = await ShowAsync();
        if (page is not PageResult) return page;
        if (!ModelState.IsValid) return Page();
        var result = await directory.CreateAsync(Geography.Level, Name, Geography.ParentId, HttpContext.RequestAborted);
        return await SavedAsync(result, nameof(Name));
    }
    public async Task<IActionResult> OnPostRenameAsync()
    {
        IActionResult page = await ShowAsync();
        if (page is not PageResult) return page;
        if (RenameId is not Guid id || !Geography.Items.Any(item => item.Id == id)) return BadRequest();
        if (!ModelState.IsValid) return Page();
        var result = await directory.RenameAsync(Geography.Level, id, RenameName, Version, HttpContext.RequestAborted);
        return await SavedAsync(result, nameof(RenameName));
    }
    public async Task<IActionResult> OnPostImportAsync()
    {
        IActionResult page = await ShowAsync();
        if (page is not PageResult) return page;
        if (!ModelState.IsValid) return Page();
        if (Upload is null || Upload.Length == 0)
        {
            ModelState.AddModelError(nameof(Upload), "Choose a CSV file."); return Page();
        }
        if (Upload.Length > GeographyImportLimits.MaximumBytes)
        {
            ModelState.AddModelError(nameof(Upload), "The CSV file must be 1 MiB or smaller."); return Page();
        }
        using MemoryStream buffer = new();
        await Upload.CopyToAsync(buffer, HttpContext.RequestAborted);
        var result = await directory.ImportAsync(buffer.ToArray(), HttpContext.RequestAborted);
        if (result.Success)
        {
            var summary = result.Value!;
            Notice = $"Added {Places(summary.RegionsAdded, "region", "regions")}, "
                + $"{Places(summary.CountiesAdded, "county", "counties")} and {Places(summary.TownsAdded, "town", "towns")}. "
                + $"{summary.TownsAlreadyPresent} {(summary.TownsAlreadyPresent == 1 ? "town was" : "towns were")} already present.";
            return RedirectToPage(new { RegionId, CountyId, ShowArchived });
        }
        return await SavedAsync(result, nameof(Upload));
    }
    private async Task<IActionResult> SavedAsync<T>(DirectoryResult<T> result, string field)
    {
        if (result.Success) return RedirectToPage(new { RegionId, CountyId, ShowArchived });
        if (result.Status is not (HttpStatusCode.BadRequest or HttpStatusCode.Conflict))
            return StatusCode((int)result.Status);
        ModelState.AddModelError(field, result.Error ?? "This change could not be saved.");
        return await ShowAsync();
    }
    private async Task<IActionResult> ShowAsync()
    {
        if (ModelState.TryGetValue(nameof(RegionId), out var region) && region.Errors.Count > 0
            || ModelState.TryGetValue(nameof(CountyId), out var county) && county.Errors.Count > 0
            || ModelState.TryGetValue(nameof(ShowArchived), out var archived) && archived.Errors.Count > 0) return BadRequest();
        var result = await directory.PageAsync(RegionId, CountyId, HttpContext.RequestAborted, ShowArchived);
        if (!result.Success) return StatusCode((int)result.Status);
        Geography = result.Value!;
        return Page();
    }
    private static string Places(int count, string singular, string plural) => $"{count} {(count == 1 ? singular : plural)}";
}
