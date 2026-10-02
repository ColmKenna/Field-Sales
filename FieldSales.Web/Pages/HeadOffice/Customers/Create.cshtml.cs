using FieldSales.Directory.Contracts;
using FieldSales.Web.Directory;
using Microsoft.AspNetCore.Mvc;

namespace FieldSales.Web.Pages.HeadOffice.Customers;

public sealed class CreateModel(DirectoryApiClient directory) : LocationFormPageModel(directory)
{
    [BindProperty] public string? CustomerName { get; set; }
    public Task<IActionResult> OnGetAsync() => FormAsync();
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return await FormAsync();
        var result = await Directory.CreateCustomerAsync(new(CustomerName, new(Name, TownId, Eircode, LocationTypeId: LocationTypeId)), HttpContext.RequestAborted);
        if (result.Success) return RedirectToPage("Detail", new { id = result.Value!.Id });
        var failure = AddSaveError(result, field => field?.StartsWith("FirstLocation.", StringComparison.Ordinal) == true
            ? field["FirstLocation.".Length..] : field == "Name" ? nameof(CustomerName) : string.Empty);
        return failure ?? await FormAsync();
    }
}
