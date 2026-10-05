using System.Net;
using FieldSales.Directory.Contracts;
using FieldSales.Web.Directory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.HeadOffice.Contacts;

public sealed class MainModel(DirectoryApiClient directory) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid ContactId { get; set; }
    [BindProperty] public string? LocationVersion { get; set; }
    [BindProperty] public Guid? ExpectedMainContactId { get; set; }
    public LocationContactsPage Location { get; private set; } = null!;
    public ContactChoice Proposed { get; private set; } = null!;
    public string? Question { get; private set; }
    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var load = await LoadAsync(id); if (load is not null) return load;
        LocationVersion = Location.LocationVersion; ExpectedMainContactId = Location.MainContact?.Id;
        return Page();
    }
    public Task<IActionResult> OnPostAsync(Guid id) => SaveAsync(id, false);
    public Task<IActionResult> OnPostConfirmAsync(Guid id) => SaveAsync(id, true);
    private async Task<IActionResult> SaveAsync(Guid id, bool confirmed)
    {
        if (ModelState.IsValid)
        {
            var result = await directory.SetMainContactAsync(id, new(ContactId, LocationVersion, ExpectedMainContactId, confirmed), HttpContext.RequestAborted);
            if (result.Success)
            {
                TempData["ContactNotice"] = "Main contact updated.";
                return RedirectToPage("/HeadOffice/Locations/Detail", new { id });
            }
            if (result.Status is not (HttpStatusCode.BadRequest or HttpStatusCode.Conflict)) return StatusCode((int)result.Status);
            ModelState.AddModelError(string.Empty, result.Error ?? "This change could not be saved.");
        }
        var load = await LoadAsync(id); return load ?? Page();
    }
    private async Task<IActionResult?> LoadAsync(Guid id)
    {
        var read = await directory.LocationContactsAsync(id, false, HttpContext.RequestAborted);
        if (!read.Success) return StatusCode((int)read.Status);
        Location = read.Value!;
        var candidate = Location.Contacts.SingleOrDefault(item => item.Contact.Id == ContactId)?.Contact;
        if (candidate is null || candidate.Status != ContactStatus.Active) return BadRequest();
        Proposed = candidate;
        Question = Location.MainContact is { } outgoing && outgoing.Id != ContactId
            ? new MainContactConfirmation(outgoing, Proposed, Location.LocationVersion).Question : null;
        return null;
    }
}
