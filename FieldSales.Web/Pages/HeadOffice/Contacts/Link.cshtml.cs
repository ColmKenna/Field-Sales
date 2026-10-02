using System.Net;
using FieldSales.Directory.Contracts;
using FieldSales.Web.Directory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.HeadOffice.Contacts;

public sealed class LinkModel(DirectoryApiClient directory) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid? LocationId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? ContactId { get; set; }
    [BindProperty] public string? LocationVersion { get; set; }
    public IReadOnlyList<ContactChoice> Contacts { get; private set; } = [];
    public IReadOnlyList<ContactLocationChoice> Locations { get; private set; } = [];
    public async Task<IActionResult> OnGetAsync()
    {
        if (LocationId is Guid id)
        {
            var read = await directory.LocationContactsAsync(id, false, HttpContext.RequestAborted);
            if (!read.Success) return StatusCode((int)read.Status);
            LocationVersion = read.Value!.LocationVersion;
        }
        return await LoadAsync();
    }
    // Selecting a shop first is a read-only GET, so the version belongs to that shop.
    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid && LocationId is Guid location && ContactId is Guid contact)
        {
            var result = await directory.LinkContactAsync(location, new(contact, LocationVersion), HttpContext.RequestAborted);
            if (result.Success)
            {
                TempData["ContactNotice"] = result.Value!.AlreadyLinked ? "This contact is already linked."
                    : result.Value.AutomaticallyMadeMain ? "First active contact made Main automatically." : "Contact linked.";
                return RedirectToPage("/HeadOffice/Locations/Detail", new { id = location });
            }
            if (result.Status is not (HttpStatusCode.BadRequest or HttpStatusCode.Conflict)) return StatusCode((int)result.Status);
            ModelState.AddModelError(result.Field ?? string.Empty, result.Error ?? "This link could not be saved.");
        }
        else ModelState.AddModelError(string.Empty, "Choose a contact and location.");
        return await LoadAsync();
    }
    private async Task<IActionResult> LoadAsync()
    {
        var contacts = await directory.ContactChoicesAsync(HttpContext.RequestAborted);
        if (!contacts.Success) return StatusCode((int)contacts.Status);
        Contacts = contacts.Value!;
        var locations = await directory.ContactLocationChoicesAsync(HttpContext.RequestAborted);
        if (!locations.Success) return StatusCode((int)locations.Status);
        Locations = locations.Value!;
        return Page();
    }
}
