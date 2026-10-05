using FieldSales.ReferenceData;
using FieldSales.Directory.Contracts;
using FieldSales.Web.Directory;
using System.Net;
using FieldSales.Web.Catalogue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.HeadOffice.ReferenceData;

public sealed class IndexModel(CatalogueApiClient catalogue, DirectoryApiClient directory) : PageModel
{
    [BindProperty(SupportsGet = true)] public string ListKey { get; set; } = ReferenceListKeys.Brands;
    [BindProperty(SupportsGet = true)] public bool ShowArchived { get; set; }
    [BindProperty] public Guid? Id { get; set; }
    [BindProperty] public string Name { get; set; } = string.Empty;
    [BindProperty] public ReferenceAction Action { get; set; }
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public string? Version { get; set; }
    public bool IsTypeList => ReferenceListKeys.IsDirectoryType(ListKey);
    public ReferenceListViewModel List { get; private set; } = null!;
    public ReferenceListItem? Confirmation { get; private set; }

    public Task<IActionResult> OnGetAsync() => ShowAsync();
    public async Task<IActionResult> OnGetConfirmAsync(Guid id)
    {
        if (IsTypeList)
        {
            var type = await directory.TypeItemAsync(ListKey, id, HttpContext.RequestAborted);
            if (!type.Success) return StatusCode((int)type.Status);
            Confirmation = type.Value!;
            Action = ReferenceRetirementPolicy.Decide(Confirmation.IsArchived, Confirmation.Usage);
            return await ShowAsync();
        }
        var read = await catalogue.ReferenceItemAsync(ListKey, id, HttpContext.RequestAborted);
        if (!read.Found) return read.FailureResult();
        Confirmation = read.Value!;
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
        if (!ModelState.IsValid) return await ShowAsync();
        if (IsTypeList)
            return await TypeResultAsync(await directory.SaveTypeAsync(ListKey, Id, new(Name, Description, Version), HttpContext.RequestAborted), nameof(Name));
        var result = await catalogue.SaveReferenceNameAsync(ListKey, Id, Name, HttpContext.RequestAborted);
        return await SaveResultAsync(result, nameof(Name));
    }
    public async Task<IActionResult> OnPostRetireAsync()
    {
        if (Id is null || ModelState.TryGetValue(nameof(Action), out var entry) && entry.Errors.Count > 0
            || !Request.Form.ContainsKey(nameof(Action)) || !Enum.IsDefined(Action)) return BadRequest();
        if (IsTypeList)
            return await TypeResultAsync(await directory.RetireTypeAsync(ListKey, Id.Value, Action, Version, HttpContext.RequestAborted), string.Empty);
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
        if (IsTypeList)
        {
            var read = await directory.TypeListAsync(ListKey, ShowArchived, HttpContext.RequestAborted);
            if (!read.Success) return StatusCode((int)read.Status);
            List = read.Value!;
        }
        else
        {
            var read = await catalogue.ReferenceListAsync(ListKey, ShowArchived, HttpContext.RequestAborted);
            if (!read.Found) return read.FailureResult();
            List = read.Value!;
        }
        List = List with { AvailableLists = ReferenceListKeys.SwitcherLists };
        if (Request.Method == "POST" && Id is Guid id && !ModelState.IsValid && ModelState.ContainsKey(nameof(Name)))
            List = List with { Items = List.Items.Select(item => item.Id == id
                ? item with { Name = Name, Description = IsTypeList ? Description : item.Description, Version = IsTypeList ? Version : item.Version } : item).ToArray() };
        return Page();
    }
    private async Task<IActionResult> TypeResultAsync<T>(DirectoryResult<T> result, string field)
    {
        if (result.Success) return RedirectToPage(new { ListKey, ShowArchived });
        if (result.Status is not (HttpStatusCode.BadRequest or HttpStatusCode.Conflict)) return StatusCode((int)result.Status);
        ModelState.AddModelError(result.Field ?? field, result.Error ?? "This change could not be saved.");
        return await ShowAsync();
    }
}
