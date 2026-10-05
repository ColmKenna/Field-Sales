using FieldSales.Directory.Contracts;
using FieldSales.Web.Directory;
using Microsoft.AspNetCore.Mvc;

namespace FieldSales.Web.Pages.HeadOffice.Contacts;

public sealed class DetailModel(DirectoryApiClient directory) : ContactFormPageModel(directory)
{
    [BindProperty] public ContactStatus Status { get; set; }
    [BindProperty] public string? Version { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? UnlinkLocationId { get; set; }
    [BindProperty(SupportsGet = true)] public bool Retire { get; set; }
    [BindProperty] public string? RemoveContactVersion { get; set; }
    [BindProperty] public string? LocationVersion { get; set; }
    [BindProperty] public List<ContactLocationVersion> AffectedLocations { get; set; } = [];
    [BindProperty] public string? ReplacementChoice { get; set; }
    [BindProperty] public Guid? ReplacementContactId { get; set; }
    [BindProperty] public string? NewName { get; set; }
    [BindProperty] public Guid? NewContactTypeId { get; set; }
    [BindProperty] public string? NewPhone { get; set; }
    [BindProperty] public string? NewEmail { get; set; }
    public ContactDetails Contact { get; private set; } = null!;
    public ContactLocationSummary? UnlinkLocation { get; private set; }
    public IReadOnlyList<ContactChoice> Replacements { get; private set; } = [];
    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var result = await Directory.ContactAsync(id, HttpContext.RequestAborted);
        if (!result.Success) return StatusCode((int)result.Status);
        Contact = result.Value!;
        Name = Contact.Name; ContactTypeId = Contact.Type.Id; Phone = Contact.Phone; Email = Contact.Email;
        Status = Contact.Status; Version = Contact.Version;
        RemoveContactVersion = Contact.Version;
        AffectedLocations = Contact.Locations.Select(location => new ContactLocationVersion(location.LocationId, location.LocationVersion)).ToList();
        return await LoadReplacementAsync();
    }
    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (ModelState.IsValid)
        {
            var result = await Directory.EditContactAsync(id, new(Name, ContactTypeId, Phone, Email, Status, Version), HttpContext.RequestAborted);
            if (result.Success) return RedirectToPage(new { id });
            var failure = AddSaveError(result); if (failure is not null) return failure;
        }
        var read = await Directory.ContactAsync(id, HttpContext.RequestAborted);
        if (!read.Success) return StatusCode((int)read.Status);
        Contact = read.Value!;
        return await LoadReplacementAsync();
    }
    public async Task<IActionResult> OnPostRemoveAsync(Guid id)
    {
        if (ModelState.IsValid)
        {
            if (UnlinkLocationId is not Guid locationId) return BadRequest();
            if (ReplacementChoice is not (null or "existing" or "new" or "none"))
                ModelState.AddModelError("ReplacementChoice", "Choose a replacement option.");
            else
            {
                var request = new RemoveLocationContactRequest(RemoveContactVersion, LocationVersion,
                    ReplacementChoice == "existing" ? ReplacementContactId : null,
                    ReplacementChoice == "new" ? new(NewName, NewContactTypeId, NewPhone, NewEmail) : null,
                    ReplacementChoice == "none", AffectedLocations);
                var result = await Directory.RemoveContactAsync(locationId, id, request, HttpContext.RequestAborted);
                if (result.Success)
                {
                    TempData["ContactNotice"] = ReplacementChoice == "none"
                        ? "Contact marked inactive across all locations. Main contact replacements are needed at the affected locations."
                        : "Contact unlinked. The location's Main contact is protected.";
                    return RedirectToPage(new { id });
                }
                var failure = AddSaveError(result, field => ReplacementChoice == "new" ? field switch
                {
                    "Name" => nameof(NewName), "ContactTypeId" => nameof(NewContactTypeId),
                    "Phone" => nameof(NewPhone), "Email" => nameof(NewEmail), _ => field
                } : field);
                if (failure is not null) return failure;
            }
        }
        return await ReloadAsync(id);
    }
    public async Task<IActionResult> OnPostRetireAsync(Guid id)
    {
        Retire = true;
        if (ModelState.IsValid)
        {
            var result = await Directory.RetireContactAsync(id, new(RemoveContactVersion, AffectedLocations), HttpContext.RequestAborted);
            if (result.Success)
            {
                TempData["ContactNotice"] = $"Contact marked inactive. {result.Value!.FlaggedLocations.Count} locations need a Main contact replacement.";
                return RedirectToPage(new { id });
            }
            var failure = AddSaveError(result); if (failure is not null) return failure;
        }
        return await ReloadAsync(id);
    }
    private async Task<IActionResult> ReloadAsync(Guid id)
    {
        var read = await Directory.ContactAsync(id, HttpContext.RequestAborted);
        if (!read.Success) return StatusCode((int)read.Status);
        Contact = read.Value!;
        Name = Contact.Name; ContactTypeId = Contact.Type.Id; Phone = Contact.Phone; Email = Contact.Email;
        Status = Contact.Status; Version = Contact.Version;
        return await LoadReplacementAsync();
    }
    private async Task<IActionResult> LoadReplacementAsync()
    {
        if (UnlinkLocationId is Guid id)
        {
            UnlinkLocation = Contact.Locations.SingleOrDefault(location => location.LocationId == id);
            if (UnlinkLocation is null) return NotFound();
            var roster = await Directory.LocationContactsAsync(id, false, HttpContext.RequestAborted);
            if (!roster.Success) return StatusCode((int)roster.Status);
            Replacements = roster.Value!.Contacts.Where(link => link.Contact.Id != Contact.Id && link.Contact.Status == ContactStatus.Active)
                .Select(link => link.Contact).ToArray();
            if (HttpMethods.IsGet(Request.Method)) LocationVersion = UnlinkLocation.LocationVersion;
        }
        return await FormAsync(Contact.Type);
    }
}
