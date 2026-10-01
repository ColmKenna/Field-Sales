using FieldSales.ReferenceData;
using FieldSales.Web.Catalogue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.HeadOffice.ReferenceData;

public sealed class IndexModel(CatalogueApiClient catalogue) : PageModel
{
    [BindProperty(SupportsGet = true)] public string ListKey { get; set; } = "brands";
    [BindProperty(SupportsGet = true)] public bool ShowArchived { get; set; }
    [BindProperty] public Guid? Id { get; set; }
    [BindProperty] public string Name { get; set; } = string.Empty;
    [BindProperty] public ReferenceAction Action { get; set; }
    public ReferenceListViewModel List { get; private set; } = null!;
    public ReferenceListItem? Confirmation { get; private set; }

    public Task<IActionResult> OnGetAsync() => ShowAsync();
    public async Task<IActionResult> OnGetConfirmAsync(Guid id)
    {
        try { Confirmation = await catalogue.ReferenceItemAsync(ListKey, id, HttpContext.RequestAborted); }
        catch (HttpRequestException) { return StatusCode(503); }
        if (Confirmation is null) return NotFound();
        Action = ReferenceRetirementPolicy.Decide(Confirmation.IsArchived, Confirmation.Usage);
        return await ShowAsync();
    }
    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ModelState.AddModelError(nameof(Name), "Enter a name.");
            return await ShowAsync();
        }
        var result = await catalogue.SaveReferenceNameAsync(ListKey, Id, Name, HttpContext.RequestAborted);
        return await SaveResultAsync(result, nameof(Name));
    }
    public async Task<IActionResult> OnPostRetireAsync()
    {
        if (Id is null || ModelState.TryGetValue(nameof(Action), out var entry) && entry.Errors.Count > 0
            || !Enum.IsDefined(Action)) return BadRequest();
        var result = await catalogue.RetireReferenceAsync(ListKey, Id.Value, Action, HttpContext.RequestAborted);
        return await SaveResultAsync(result, string.Empty);
    }
    private async Task<IActionResult> SaveResultAsync(ReferenceSaveResult result, string field)
    {
        if (result.Status == ReferenceSaveStatus.Saved)
            return RedirectToPage(new { ListKey, ShowArchived });
        if (result.Status == ReferenceSaveStatus.Missing) return NotFound();
        if (result.Status == ReferenceSaveStatus.Unavailable) return StatusCode(503);
        ModelState.AddModelError(field, result.Error ?? "This change could not be saved. Reload before continuing.");
        return await ShowAsync();
    }
    private async Task<IActionResult> ShowAsync()
    {
        try
        {
            var list = await catalogue.ReferenceListAsync(ListKey, ShowArchived, HttpContext.RequestAborted);
            if (list is null) return NotFound();
            List = list;
            if (Request.Method == "POST" && Id is Guid id && !ModelState.IsValid && ModelState.ContainsKey(nameof(Name)))
                List = List with { Items = List.Items.Select(item => item.Id == id ? item with { Name = Name } : item).ToArray() };
            return Page();
        }
        catch (HttpRequestException) { return StatusCode(503); }
    }
}
