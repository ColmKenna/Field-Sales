using FieldSales.Directory.Contracts;
using FieldSales.Web.Directory;
using Microsoft.AspNetCore.Mvc;

namespace FieldSales.Web.Pages.HeadOffice.Contacts;

public sealed class CreateModel(DirectoryApiClient directory) : ContactFormPageModel(directory)
{
    [BindProperty] public List<Guid> LocationIds { get; set; } = [];
    public IReadOnlyList<ContactLocationChoice> Locations { get; private set; } = [];
    public async Task<IActionResult> OnGetAsync(Guid? locationId)
    {
        if (locationId is Guid id) LocationIds.Add(id);
        return await LoadAsync();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid)
        {
            var result = await Directory.CreateContactAsync(new(Name, ContactTypeId, LocationIds, Phone, Email), HttpContext.RequestAborted);
            if (result.Success)
            {
                var main = result.Value!.Locations.Where(location => location.IsMain).Select(location => location.Name).ToArray();
                if (main.Length > 0) TempData["ContactNotice"] = "First active contact made Main automatically at " + string.Join(", ", main) + ".";
                return RedirectToPage("Detail", new { id = result.Value.Id });
            }
            var failure = AddSaveError(result); if (failure is not null) return failure;
        }
        return await LoadAsync();
    }
    private async Task<IActionResult> LoadAsync()
    {
        var locations = await Directory.ContactLocationChoicesAsync(HttpContext.RequestAborted);
        if (!locations.Success) return StatusCode((int)locations.Status);
        Locations = locations.Value!;
        return await FormAsync();
    }
}
