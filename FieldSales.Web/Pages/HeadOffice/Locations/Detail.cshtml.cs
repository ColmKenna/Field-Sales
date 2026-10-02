using FieldSales.Directory.Contracts;
using FieldSales.Web.Directory;
using Microsoft.AspNetCore.Mvc;

namespace FieldSales.Web.Pages.HeadOffice.Locations;

public sealed class DetailModel(DirectoryApiClient directory) : LocationFormPageModel(directory)
{
    [BindProperty] public string? Version { get; set; }
    public LocationDetails Location { get; private set; } = null!;
    [BindProperty(SupportsGet = true)] public bool ShowInactive { get; set; }
    public LocationContactsPage Contacts { get; private set; } = null!;
    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var result = await Directory.LocationAsync(id, HttpContext.RequestAborted);
        if (!result.Success) return StatusCode((int)result.Status);
        Location = result.Value!;
        Name = Location.Name; TownId = Location.Town.Id; Eircode = Location.Eircode; Version = Location.Version;
        LocationTypeId = Location.Type?.Id;
        return await LoadContactsAsync(id);
    }
    public Task<IActionResult> OnPostAsync(Guid id) => SaveAsync(id, false);
    public Task<IActionResult> OnPostConfirmAsync(Guid id) => SaveAsync(id, true);
    private async Task<IActionResult> SaveAsync(Guid id, bool confirmed)
    {
        if (ModelState.IsValid)
        {
            var result = await Directory.EditLocationAsync(id, new(Name, TownId, Eircode, Version, confirmed, LocationTypeId), HttpContext.RequestAborted);
            if (result.Success) return RedirectToPage(new { id });
            var failure = AddSaveError(result);
            if (failure is not null) return failure;
        }
        var read = await Directory.LocationAsync(id, HttpContext.RequestAborted);
        if (!read.Success) return StatusCode((int)read.Status);
        Location = read.Value!;
        return await LoadContactsAsync(id);
    }
    private async Task<IActionResult> LoadContactsAsync(Guid id)
    {
        var contacts = await Directory.LocationContactsAsync(id, ShowInactive, HttpContext.RequestAborted);
        if (!contacts.Success) return StatusCode((int)contacts.Status);
        Contacts = contacts.Value!;
        return await FormAsync(Location.Town, Location.Type);
    }
}
