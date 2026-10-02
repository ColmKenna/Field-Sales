using System.Net;
using FieldSales.Directory.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Directory;

public abstract class LocationFormPageModel(DirectoryApiClient directory) : PageModel
{
    protected DirectoryApiClient Directory => directory;
    [BindProperty] public string? Name { get; set; }
    [BindProperty] public Guid? TownId { get; set; }
    [BindProperty] public string? Eircode { get; set; }
    public IReadOnlyList<TownChoice> Towns { get; private set; } = [];
    public bool DuplicateWarning { get; private set; }

    protected async Task<IActionResult> FormAsync(TownChoice? existingTown = null)
    {
        var result = await Directory.TownChoicesAsync(HttpContext.RequestAborted);
        if (!result.Success) return StatusCode((int)result.Status);
        Towns = existingTown is not null && !result.Value!.Any(town => town.Id == existingTown.Id)
            ? result.Value!.Append(existingTown).ToArray() : result.Value!;
        return Page();
    }

    protected IActionResult? AddSaveError<T>(DirectoryResult<T> result, Func<string?, string>? fieldMap = null)
    {
        if (result.Status is not (HttpStatusCode.BadRequest or HttpStatusCode.Conflict)) return StatusCode((int)result.Status);
        DuplicateWarning = result.RequiresDuplicateConfirmation;
        ModelState.AddModelError(fieldMap?.Invoke(result.Field) ?? result.Field ?? string.Empty,
            result.Error ?? "This change could not be saved.");
        return null;
    }
}
