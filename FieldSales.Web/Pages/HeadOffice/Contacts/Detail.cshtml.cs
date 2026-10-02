using FieldSales.Directory.Contracts;
using FieldSales.Web.Directory;
using Microsoft.AspNetCore.Mvc;

namespace FieldSales.Web.Pages.HeadOffice.Contacts;

public sealed class DetailModel(DirectoryApiClient directory) : ContactFormPageModel(directory)
{
    [BindProperty] public ContactStatus Status { get; set; }
    [BindProperty] public string? Version { get; set; }
    public ContactDetails Contact { get; private set; } = null!;
    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var result = await Directory.ContactAsync(id, HttpContext.RequestAborted);
        if (!result.Success) return StatusCode((int)result.Status);
        Contact = result.Value!;
        Name = Contact.Name; ContactTypeId = Contact.Type.Id; Phone = Contact.Phone; Email = Contact.Email;
        Status = Contact.Status; Version = Contact.Version;
        return await FormAsync(Contact.Type);
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
        return await FormAsync(Contact.Type);
    }
}
