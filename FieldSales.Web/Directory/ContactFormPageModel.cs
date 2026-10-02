using System.Net;
using FieldSales.Directory.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Directory;

public abstract class ContactFormPageModel(DirectoryApiClient directory) : PageModel
{
    protected DirectoryApiClient Directory => directory;
    [BindProperty] public string? Name { get; set; }
    [BindProperty] public Guid? ContactTypeId { get; set; }
    [BindProperty] public string? Phone { get; set; }
    [BindProperty] public string? Email { get; set; }
    public IReadOnlyList<DirectoryTypeChoice> Types { get; private set; } = [];
    protected async Task<IActionResult> FormAsync(DirectoryTypeChoice? existingType = null)
    {
        var result = await Directory.ContactTypeChoicesAsync(HttpContext.RequestAborted);
        if (!result.Success) return StatusCode((int)result.Status);
        Types = existingType is not null && !result.Value!.Any(type => type.Id == existingType.Id)
            ? result.Value!.Append(existingType).ToArray() : result.Value!;
        return Page();
    }
    protected IActionResult? AddSaveError<T>(DirectoryResult<T> result)
    {
        if (result.Status is not (HttpStatusCode.BadRequest or HttpStatusCode.Conflict)) return StatusCode((int)result.Status);
        ModelState.AddModelError(result.Field ?? string.Empty, result.Error ?? "This change could not be saved.");
        return null;
    }
}
